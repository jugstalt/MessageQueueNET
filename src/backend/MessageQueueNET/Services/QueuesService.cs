using MessageQueueNET.Exceptions;
﻿using MessageQueueNET.Extensions;
using MessageQueueNET.Models;
using MessageQueueNET.Services.Abstraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MessageQueueNET.Services;

public class QueuesService
{
    private readonly ConcurrentDictionary<string, Queue> _queues;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, QueueItem>> _unconfirmedItems;
    private readonly MessageQueueLimitsOptions _limits;
    private readonly ILogger<QueuesService> _logger;
    private readonly object _createSyncRoot = new object();

    public QueuesService(IOptions<MessageQueueLimitsOptions> limits,
                         ILogger<QueuesService>? logger = null)
    {
        _logger = logger ?? NullLogger<QueuesService>.Instance;
        _queues = new ConcurrentDictionary<string, Queue>();
        _unconfirmedItems = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, QueueItem>>();
        _limits = limits.Value;
    }

    public MessageQueueLimitsOptions Limits => _limits;

    public IEnumerable<Queue> GetQueues(string queueNamePattern, bool forAccess = true, bool createIfNotExists = true)
    {
        if (!queueNamePattern.IsPattern())
        {
            if (!createIfNotExists && !_queues.ContainsKey(queueNamePattern))
            {
                return Array.Empty<Queue>();
            }

            return new Queue[] { GetQueue(queueNamePattern, forAccess) };
        }

        return MatchingQueueNames(queueNamePattern)
                    .ToArray()
                    .Select(name => GetQueue(name, forAccess))
                    .ToList();
    }

    // each existing queue name once, even if it fits more than one pattern
    private IEnumerable<string> MatchingQueueNames(string queueNamePattern)
    {
        var keys = _queues.Keys;
        var found = new HashSet<string>();

        foreach (var pattern in queueNamePattern.Split(',').Select(p => p.Trim()))
        {
            string regexPattern = pattern.ToRegexPattern();

            foreach (var key in keys)
            {
                if ((key == pattern || key.FitsRegexPattern(regexPattern)) && found.Add(key))
                {
                    yield return key;
                }
            }
        }
    }

    public Queue GetQueue(string queueName, bool forAccess = true)
        => GetQueue(queueName, forAccess, ignoreLimits: false);

    private Queue GetQueue(string queueName, bool forAccess, bool ignoreLimits)
    {
        if (!queueName.IsValidQueueName())
        {
            throw new MessageQueueException($"Invalid queue name. Allowed are lowercase characters, numbers, minus, underscore and single points between them (must start with a character or number, max. {StringExtensions.MaxQueueNameLength} characters)");
        }

        if (!_queues.TryGetValue(queueName, out var queue) || queue.IsRemoved)
        {
            // creating is synchronized, so parallel requests cannot exceed MaxQueues
            lock (_createSyncRoot)
            {
                var replaceRemoved = _queues.TryGetValue(queueName, out queue) && queue.IsRemoved;

                if (queue is null || replaceRemoved)
                {
                    if (!replaceRemoved
                        && !ignoreLimits
                        && _limits.MaxQueues > 0
                        && _queues.Count >= _limits.MaxQueues)
                    {
                        throw new MessageQueueException($"Maximum number of queues ({_limits.MaxQueues}) reached");
                    }

                    queue = new Queue(queueName);
                    _queues[queueName] = queue;
                }
            }
        }

        if (forAccess == true)
        {
            queue.LastAccessUTC = DateTime.UtcNow;
        }

        return queue;
    }

    public bool QueueExists(string queueName)
    {
        return _queues.ContainsKey(queueName);
    }

    public bool AnyQueueExists(string queueNamePattern)
    {
        if (!queueNamePattern.IsPattern())
        {
            return _queues.ContainsKey(queueNamePattern);
        }

        return MatchingQueueNames(queueNamePattern).Any();
    }

    // true, if the queue existed and was removed
    public bool RemoveQueue(string queueName)
    {
        var removed = _queues.TryRemove(queueName, out _);
        _unconfirmedItems.TryRemove(queueName, out _);

        return removed;
    }

    // Removes exactly this instance: a queue that was created again in the meantime stays untouched
    public bool RemoveQueue(Queue queue)
    {
        if (!_queues.TryRemove(new KeyValuePair<string, Queue>(queue.Name, queue)))
        {
            return false;
        }

        _unconfirmedItems.TryRemove(queue.Name, out _);

        return true;
    }

    public bool RemoveQueueMessages(Queue queue)
    {
        queue.Clear();

        return true;
    }

    public bool RemoveUnconfirmedQueueItems(Queue queue)
    {
        if (!_unconfirmedItems.ContainsKey(queue.Name))
        {
            return true;
        }
        return _unconfirmedItems.TryRemove(queue.Name, out _);
    }

    public IEnumerable<string> QueueNames => _queues.Keys.ToArray();

    public IEnumerable<Queue> Queues => _queues.Values.ToArray();

    #region Handle Processing Confirmations

    public bool AddToUnconfirmedMessage(Queue queue, QueueItem queueItem)
    {
        try
        {
            if (queueItem != null)
            {
                var unconfirmed = _unconfirmedItems.GetOrAdd(queue.Name, (key) => new ConcurrentDictionary<Guid, QueueItem>());

                return unconfirmed.TryAdd(queueItem.Id, queueItem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding unconfirmed item to queue {Queue}", queue.Name);
        }

        return false;
    }

    // Removes an item from the unconfirmed items without confirming it (eg. if dequeue could not be completed)
    public bool RemoveFromUnconfirmedMessages(Queue queue, Guid messageId)
        => _unconfirmedItems.TryGetValue(queue.Name, out var unconfirmedItems)
           && unconfirmedItems.TryRemove(messageId, out _);

    async public Task<bool> ReEnqueueUnconfirmedMessages(Queue queue, IQueuesPersistService persist)
    {
        try
        {
            if (_unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
            {
                foreach (var queueItem in unconfirmedItems.Values
                                                          .ToArray()
                                                          .Where(i => (DateTime.UtcNow - i.UnconfirmedSinceUTC).TotalSeconds > queue.Properties.ConfirmationPeriodSeconds))
                {
                    if (unconfirmedItems.TryRemove(queueItem.Id, out QueueItem? item))
                    {
                        item.DequeuedUTC = null;
                        item.DequeuingClientId = "";
                        item.DequeuingUser = "";

                        if (await persist.RemoveUnconfirmedQueueItem(queue.Name, item.Id) &&
                            await persist.PersistQueueItem(queue.Name, item))
                        {
                            queue.Enqueue(item);
                        }
                        else
                        {
                            unconfirmedItems.TryAdd(queueItem.Id, queueItem);
                        }
                    }

                    queue.SetModified();
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-enqueuing unconfirmed items of queue {Queue}", queue.Name);
            return false;
        }
    }

    // Only the user that dequeued an item may confirm it. Without authentication (user is null/empty)
    // or for items dequeued without a user, there is no identity to compare.
    public bool IsConfirmingUserAllowed(Queue queue, Guid messageId, string? user)
        => string.IsNullOrEmpty(user)
           || !_unconfirmedItems.TryGetValue(queue.Name, out var unconfirmedItems)
           || !unconfirmedItems.TryGetValue(messageId, out var item)
           || string.IsNullOrEmpty(item.DequeuingUser)
           || string.Equals(item.DequeuingUser, user, StringComparison.Ordinal);

    public bool ConfirmDequeuedMessage(Queue queue, Guid messageId)
    {
        try
        {
            if (_unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
            {
                if (unconfirmedItems.TryGetValue(messageId, out _))
                {
                    return unconfirmedItems.TryRemove(messageId, out _);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming message {MessageId} of queue {Queue}", messageId, queue.Name);
        }

        return false;
    }

    // null: confirmation is not used for this queue (counts and client lists are null then)
    private static bool ConfirmationDisabled(Queue queue)
        => queue?.Properties == null || queue.Properties.ConfirmationPeriodSeconds <= 0;

    public int? UnconfirmedMessagesCount(Queue queue, string? clientId)
    {
        if (ConfirmationDisabled(queue))
        {
            return null;
        }

        if (_unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
        {
            if (clientId == null)
            {
                return unconfirmedItems.Count;
            }
            else
            {
                return unconfirmedItems.Where(i => clientId.Equals(i.Value?.DequeuingClientId)).Count();
            }
        }

        return 0;
    }

    public int? DequeuingClientsCount(Queue queue)
    {
        if (ConfirmationDisabled(queue))
        {
            return null;
        }

        if (_unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
        {
            return unconfirmedItems.Select(i => i.Value?.DequeuingClientId).Distinct().Count();
        }

        return 0;
    }

    public IDictionary<string, int>? UnconfirmedMessagesByClient(Queue queue)
    {
        if (ConfirmationDisabled(queue))
        {
            return null;
        }

        var dict = new Dictionary<string, int>();

        if (_unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
        {
            foreach (var item in unconfirmedItems.Values.ToArray())
            {
                var clientId = item?.DequeuingClientId ?? String.Empty;
                dict[clientId] = dict.GetValueOrDefault(clientId) + 1;
            }
        }

        return dict;
    }

    public IEnumerable<Client.Models.MessageResult>? UnconfirmedItems(Queue queue)
    {
        try
        {
            if (queue.Properties.ConfirmationPeriodSeconds >= 0 &&
                _unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
            {
                var items = unconfirmedItems.Values
                                       .ToArray()
                                       .Select(i => new Client.Models.MessageResult()
                                       {
                                           Id = i.Id,
                                           Value = i.Message,
                                           CreationDateUTC = i.CreationDateUTC,
                                           DequeuingClientId = i.DequeuingClientId
                                       });

                return items.Count() > 0 ? items : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading unconfirmed items of queue {Queue}", queue.Name);
        }

        return null;
    }

    #endregion

    #region Restore

    public bool Restore(string queueName,
                        QueueProperties properties,
                        IEnumerable<QueueItem> items,
                        IEnumerable<QueueItem> unconfirmedItems)
    {
        try
        {
            var queue = GetQueue(queueName, forAccess: true, ignoreLimits: true);
            queue.Properties = properties;
            queue.Clear();

            if (items != null)
            {
                foreach (var item in items.OrderBy(i => i.CreationDateUTC))
                {
                    queue.Enqueue(item);
                }
            }

            if (unconfirmedItems != null && unconfirmedItems.Count() > 0)
            {
                var unconfirmed = _unconfirmedItems.GetOrAdd(queue.Name, (key) => new ConcurrentDictionary<Guid, QueueItem>());

                foreach (var item in unconfirmedItems)
                {
                    unconfirmed.TryAdd(item.Id, item);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring queue {Queue}", queueName);
            return false;
        }
    }

    #endregion
}
