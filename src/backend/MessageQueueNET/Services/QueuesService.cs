using MessageQueueNET.Exceptions;
﻿using MessageQueueNET.Extensions;
using MessageQueueNET.Models;
using MessageQueueNET.Services.Abstraction;
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

    public QueuesService(IOptions<MessageQueueLimitsOptions> limits)
    {
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

        List<Queue> queues = new List<Queue>();

        foreach (var pattern in queueNamePattern.Split(',').Select(p => p.Trim()))
        {
            string regexPattern = pattern.ToRegexPattern();

            foreach (var key in _queues.Keys)
            {
                if (key == pattern || key.FitsRegexPattern(regexPattern))
                {
                    if (queues.Any(q => q.Name == key) == false)
                    {
                        queues.Add(GetQueue(key, forAccess));
                    }
                }
            }
        }

        return queues;
    }

    public Queue GetQueue(string queueName, bool forAccess = true)
        => GetQueue(queueName, forAccess, ignoreLimits: false);

    private Queue GetQueue(string queueName, bool forAccess, bool ignoreLimits)
    {
        if (!queueName.IsValidQueueName())
        {
            throw new MessageQueueException($"Invalid queue name. Allowed are lowercase characters, numbers, minus, underscore and single points between them (must start with a character or number, max. {StringExtensions.MaxQueueNameLength} characters)");
        }

        if (!ignoreLimits
            && _limits.MaxQueues > 0
            && !_queues.ContainsKey(queueName)
            && _queues.Count >= _limits.MaxQueues)
        {
            throw new MessageQueueException($"Maximum number of queues ({_limits.MaxQueues}) reached");
        }

        var queue = _queues.GetOrAdd(queueName, (key) => new Queue(queueName));

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

        foreach (var pattern in queueNamePattern.Split(',').Select(x => x.Trim()))
        {
            string regexPattern = pattern.ToRegexPattern();
            foreach (var key in _queues.Keys)
            {
                if (key == pattern || key.FitsRegexPattern(regexPattern))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool RemoveQueue(string queueName)
    {
        try
        {
            _queues.TryRemove(queueName, out _);
            _unconfirmedItems.TryRemove(queueName, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems);

            return true;
        }
        catch
        {
            return false;
        }
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
        catch
        {

        }

        return false;
    }

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
        catch
        {
            return false;
        }
    }

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
        catch
        {

        }

        return false;
    }

    public int? UnconfirmedMessagesCount(Queue queue, string? clientId)
    {
        if (queue?.Properties == null || queue.Properties.ConfirmationPeriodSeconds <= 0)
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
        if (queue?.Properties == null || queue.Properties.ConfirmationPeriodSeconds <= 0)
        {
            return null;
        }

        if (_unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
        {
            return unconfirmedItems.Select(i => i.Value?.DequeuingClientId).Distinct().Count();
        }

        return null;
    }

    public IDictionary<string, int>? UnconfirmedMessagesByClient(Queue queue)
    {
        if (queue?.Properties == null || queue.Properties.ConfirmationPeriodSeconds <= 0)
        {
            return null;
        }

        if (_unconfirmedItems.TryGetValue(queue.Name, out ConcurrentDictionary<Guid, QueueItem>? unconfirmedItems))
        {
            var dict = new Dictionary<string, int>();
            var cliendIds = unconfirmedItems.Select(i => i.Value?.DequeuingClientId ?? String.Empty).Distinct();

            foreach (var clientId in cliendIds)
            {
                dict[clientId] = unconfirmedItems
                    .Count(i => (i.Value?.DequeuingClientId ?? String.Empty) == clientId);
            }

            return dict;
        }

        return null;
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
        catch
        {

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
        catch
        {
            return false;
        }
    }

    #endregion
}
