using MessageQueueNET.Client;
using MessageQueueNET.Client.Models;
using MessageQueueNET.Core.Concurrency;
using MessageQueueNET.Exceptions;
using MessageQueueNET.Extensions;
using MessageQueueNET.Middleware.Authentication;
using MessageQueueNET.Models;
using MessageQueueNET.Models.Authentication;
using MessageQueueNET.Services;
using MessageQueueNET.Services.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageQueueNET.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [RequiredRole(ClientRole.Admin)]
    public class QueueController : ControllerBase
    {
        private readonly QueuesService _queues;
        private readonly IQueuesPersistService _persist;
        private readonly ILogger<QueueController> _logger;

        public QueueController(QueuesService queues,
                               IQueuesPersistService persist,
                               ILogger<QueueController> logger)
        {
            _queues = queues;
            _persist = persist;
            _logger = logger;
        }

        [HttpPost]
        [Route("dequeue/{idPattern}")]
        [RequiredRole(ClientRole.Write)]
        async public Task<MessagesResult> Dequeue(string idPattern, int count = 1, bool register = false)
        {
            try
            {
                if (register == true
                    && idPattern.IsPattern() == false
                    && _queues.QueueExists(idPattern) == false)
                {
                    _queues.GetQueue(idPattern);  // register Queue
                }

                count = Math.Max(1, _queues.Limits.MaxDequeueCount > 0
                    ? Math.Min(count, _queues.Limits.MaxDequeueCount)
                    : count);

                var queryQueues = _queues.GetQueues(idPattern);
                var result = new MessagesResult(queryQueues.CalcHashCode());
                var modifiedQueues = new List<Queue>();
                var clientId = this.Request.GetClientId();

                if (queryQueues.Any() == false)
                {
                    return result;
                }

                List<MessageResult> items = new List<MessageResult>();
                List<Queue> queueBag = new List<Queue>();
                var persistError = false;

                using (var mutex = await MessageQueueFuzzyMutexAsync.LockAsync("dequeue", queryQueues.Select(q => q.Name).ToArray()))
                {
                    while (items.Count() < count)
                    {
                        var queue = queryQueues.QueueWithOldestDequeueAbleItem(_queues, clientId, queueBag);
                        if (queue is null)
                        {
                            break;
                        }

                        if (queue.TryDequeue(out QueueItem? item))
                        {
                            modifiedQueues.Add(queue);

                            if (!item!.IsValid(queue))
                            {
                                // expired => drop it (never move it to the unconfirmed items)
                                await _persist.RemoveQueueItem(queue.Name, item.Id);
                                continue;
                            }

                            QueueItem? unconfirmedItem = null;
                            var persisted = true;

                            if (queue.Properties.ConfirmationPeriodSeconds > 0)
                            {
                                unconfirmedItem = item.CloneForUnconfirmed(clientId);
                                persisted = await _persist.PersistUnconfirmedQueueItem(queue.Name, unconfirmedItem);

                                if (persisted)
                                {
                                    _queues.AddToUnconfirmedMessage(queue, unconfirmedItem);
                                }
                            }

                            persisted = persisted && await _persist.RemoveQueueItem(queue.Name, item.Id);

                            if (!persisted)
                            {
                                // not delivered => put the message back, it must not get lost
                                if (unconfirmedItem != null)
                                {
                                    _queues.RemoveFromUnconfirmedMessages(queue, item.Id);
                                    await _persist.RemoveUnconfirmedQueueItem(queue.Name, item.Id);
                                }

                                queue.Enqueue(item);
                                persistError = true;
                                break;
                            }

                            items.Add(new MessageResult()
                            {
                                Queue = queue.Name,
                                Id = item.Id,
                                Value = item.Message,
                                RequireConfirmation = queue.Properties.RequireConfirmation() ? true : null,
                                ConfirmationPeriod = queue.Properties.RequireConfirmation() ? queue.Properties.ConfirmationPeriodSeconds : null,
                            });
                        }
                    }

                    if (persistError)
                    {
                        _logger.LogError("Dequeue: unable to persist the dequeue state, the message was put back to the queue");

                        if (items.Count == 0)
                        {
                            throw new Exception("Unable to persist dequeue state");
                        }
                    }

                    result.Messages = items;

                    if (modifiedQueues.Count() > 0)
                    {
                        modifiedQueues.SetModified();
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                return new MessagesResult().AddExceptionMessage(ex, _logger);
            }
        }

        [HttpPost]
        [Route("confirmdequeue/{id}")]
        [RequiredRole(ClientRole.Write)]
        async public Task<ApiResult> ConfirmDequeue(string id, Guid messageId)
        {
            try
            {
                var queue = _queues.GetQueues(id, createIfNotExists: false).FirstOrDefault();
                if (queue is null || id.IsPattern())
                {
                    throw new MessageQueueException($"Queue {id} not found");
                }

                var result = new ApiResult(queue.CalcHashCode());

                if (queue.Properties.ConfirmationPeriodSeconds > 0)
                {
                    if (await _persist.RemoveUnconfirmedQueueItem(queue.Name, messageId))
                    {
                        result.Success = _queues.ConfirmDequeuedMessage(queue, messageId);
                        queue.SetModified();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ApiResult().AddExceptionMessage(ex, _logger);
            }
        }

        [HttpPut]
        [Route("enqueue/{id}")]
        [RequiredRole(ClientRole.Write)]
        async public Task<ApiResult> Enqueue(string id, IEnumerable<string> messages)
        {
            try
            {
                var messageArray = messages?.Select(m => m ?? "").ToArray()
                    ?? throw new MessageQueueException("No messages");

                var limits = _queues.Limits;
                if (limits.MaxMessageBytes > 0
                    && messageArray.Any(m => Encoding.UTF8.GetByteCount(m) > limits.MaxMessageBytes))
                {
                    throw new MessageQueueException($"Message exceeds the maximum size of {limits.MaxMessageBytes} bytes");
                }

                var items = messageArray.Select(m => new QueueItem() { Message = m }).ToArray();
                Queue queue;

                // a queue that is removed in parallel (lifetime) does not accept items => get it again
                for (int attempt = 0; ; attempt++)
                {
                    queue = _queues.GetQueue(id);

                    if (queue.Properties.SuspendEnqueue == true)
                    {
                        throw new MessageQueueException($"Enqueue suspended");
                    }

                    if (limits.MaxQueueLength > 0
                        && queue.Count + items.Length > limits.MaxQueueLength)
                    {
                        throw new MessageQueueException($"Maximum queue length ({limits.MaxQueueLength}) exceeded");
                    }

                    // all or nothing: persist every item first, enqueue only if all are persisted
                    var persisted = new List<QueueItem>();
                    foreach (var item in items)
                    {
                        if (!await _persist.PersistQueueItem(queue.Name, item))
                        {
                            foreach (var persistedItem in persisted)
                            {
                                await _persist.RemoveQueueItem(queue.Name, persistedItem.Id);
                            }

                            throw new Exception($"Error when persisting item");
                        }

                        persisted.Add(item);
                    }

                    if (queue.EnqueueRange(items))
                    {
                        break;
                    }

                    if (attempt >= 2)
                    {
                        throw new Exception("Queue was removed while enqueuing");
                    }
                }
                queue.SetModified();

                return new ApiResult(queue.CalcHashCode());
            }
            catch (Exception ex)
            {
                return new ApiResult().AddExceptionMessage(ex, _logger);
            }
        }

        [HttpDelete]
        [Route("deletemessage/{id}")]
        [RequiredRole(ClientRole.Admin)]
        async public Task<ApiResult> DeleteMessage(string id, Guid messageId)
        {
            try
            {
                var queue = _queues.GetQueues(id, createIfNotExists: false).FirstOrDefault();
                if (queue is null || id.IsPattern())
                {
                    throw new MessageQueueException($"Queue {id} not found");
                }

                var success = queue.TryDelete(messageId);

                if (success)
                {
                    await _persist.RemoveQueueItem(queue.Name, messageId);
                    queue.SetModified();
                }

                return new ApiResult(queue.CalcHashCode()) { Success = success };
            }
            catch (Exception ex)
            {
                return new ApiResult().AddExceptionMessage(ex, _logger);
            }
        }

        [HttpGet]
        [Route("all/{idPattern}")]
        [RequiredRole(ClientRole.Read)]
        public MessagesResult AllMessages(string idPattern, int max = 0, bool unconfirmedOnly = false)
        {
            try
            {
                if (_queues.AnyQueueExists(idPattern))
                {
                    var messages = new List<MessageResult>();
                    var unconfirmedMessages = new List<MessageResult>();

                    var queues = _queues.GetQueues(idPattern);
                    var result = new MessagesResult(queues.CalcHashCode());

                    foreach (var queue in queues)
                    {
                        if (unconfirmedOnly == false)
                        {
                            messages.AddRange(queue
                                .Where(message => message.IsValid(queue))
                                .Select(message => new MessageResult()
                                {
                                    Queue = queue.Name,
                                    Id = message.Id,
                                    Value = message.Message,
                                    CreationDateUTC = message.CreationDateUTC,
                                    RequireConfirmation = queue.Properties.ConfirmationPeriodSeconds > 0 ? true : null,
                                    ConfirmationPeriod = queue.Properties.ConfirmationPeriodSeconds > 0 ? queue.Properties.ConfirmationPeriodSeconds : null,
                                }));

                            if (max > 0)
                            {
                                messages = messages.Take(max).ToList();
                            }
                        }

                        unconfirmedMessages.AddRange(_queues.UnconfirmedItems(queue) ?? Array.Empty<MessageResult>());

                        if (max > 0)
                        {
                            unconfirmedMessages = unconfirmedMessages.Take(max).ToList();
                        }
                    }

                    result.Messages = messages;
                    result.UnconfirmedMessages = unconfirmedMessages;

                    return result;
                }
            }
            catch (Exception ex)
            {
                return new MessagesResult().AddExceptionMessage(ex, _logger);
            }

            return new MessagesResult(0);
        }

        [HttpGet]
        [Route("length/{idPattern}")]
        [RequiredRole(ClientRole.Read)]
        public QueueLengthResult Length(string idPattern)
        {
            try
            {
                if (_queues.AnyQueueExists(idPattern))
                {
                    var queues = _queues.GetQueues(idPattern);
                    var result = new QueueLengthResult(queues.CalcHashCode());

                    var queueLengthItems = new Dictionary<string, QueueLengthItem>();

                    foreach (var queue in queues)
                    {
                        queueLengthItems[queue.Name] = new QueueLengthItem()
                        {
                            QueueLength = queue.Where(item => item.IsValid(queue))
                                               .Count(),
                            UnconfirmedItems = _queues.UnconfirmedMessagesCount(queue, null)
                        };
                    }

                    result.Queues = queueLengthItems;

                    return result;
                }
            }
            catch (Exception ex)
            {
                return new QueueLengthResult().AddExceptionMessage(ex, _logger);
            }

            return new QueueLengthResult(0);
        }

        [HttpDelete]
        [Route("remove/{idPattern}")]
        [RequiredRole(ClientRole.Write, patternRole: ClientRole.Admin)]
        async public Task<ApiResult> Remove(string idPattern, RemoveType removeType = RemoveType.Queue)
        {
            try
            {
                if (_queues.AnyQueueExists(idPattern))
                {
                    var queues = _queues.GetQueues(idPattern);
                    var result = new ApiResult(queues.CalcHashCode());

                    foreach (var queue in queues)
                    {
                        if (removeType == RemoveType.Messages)
                        {
                            if (await _persist.RemoveQueueMessages(queue.Name))
                            {
                                result.Success &= _queues.RemoveQueueMessages(queue);
                                queue.SetModified();
                            }
                            else { result.Success = false; }
                        }
                        else if (removeType == RemoveType.UnconfirmedMessages)
                        {
                            if (await _persist.RemoveQueueUnconfirmedMessages(queue.Name))
                            {
                                result.Success &= _queues.RemoveUnconfirmedQueueItems(queue);
                                queue.SetModified();
                            }
                            else { result.Success = false; }
                        }
                        else
                        {
                            if (await _persist.RemoveQueue(queue.Name))
                            {
                                _queues.RemoveQueue(queue.Name);
                            }
                            else { result.Success = false; }
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                return new ApiResult().AddExceptionMessage(ex, _logger);
            }

            return new ApiResult(0);
        }

        [HttpPut]
        [Route("register/{idPattern}")]
        [RequiredRole(ClientRole.Write, patternRole: ClientRole.Admin)]
        async public Task<QueuePropertiesResult> Register(
                             string idPattern,
                             int? lifetimeSeconds = null,
                             int? itemLifetimeSeconds = null,
                             int? confirmationPeriodSeconds = null,
                             int? maxUnconfirmedItems = null,
                             MaxUnconfirmedItemsStrategy? maxUnconfirmedItemsStrategy = null,
                             bool? suspendEnqueue = null,
                             bool? suspendDequeue = null)
        {
            try
            {
                var queues = _queues.GetQueues(idPattern);

                foreach (var queue in queues)
                {
                    queue.Properties.LifetimeSeconds = lifetimeSeconds.GetValueOrDefault(queue.Properties.LifetimeSeconds);
                    queue.Properties.ItemLifetimeSeconds = itemLifetimeSeconds.GetValueOrDefault(queue.Properties.ItemLifetimeSeconds);
                    queue.Properties.ConfirmationPeriodSeconds = confirmationPeriodSeconds.GetValueOrDefault(queue.Properties.ConfirmationPeriodSeconds);
                    queue.Properties.MaxUnconfirmedItems = maxUnconfirmedItems.GetValueOrDefault(queue.Properties.MaxUnconfirmedItems);
                    queue.Properties.MaxUnconfirmedItemsStrategy = maxUnconfirmedItemsStrategy.GetValueOrDefault(queue.Properties.MaxUnconfirmedItemsStrategy);
                    queue.Properties.SuspendEnqueue = suspendEnqueue.GetValueOrDefault(queue.Properties.SuspendEnqueue);
                    queue.Properties.SuspendDequeue = suspendDequeue.GetValueOrDefault(queue.Properties.SuspendDequeue);

                    if (!await _persist.PersistQueueProperties(queue))
                    {
                        throw new Exception($"Error when persisting properties of queue {queue.Name}");
                    }
                }

                queues.SetModified();

                return QueueProperties(idPattern);
            }
            catch (Exception ex)
            {
                return new QueuePropertiesResult().AddExceptionMessage(ex, _logger);
            }
        }

        [HttpGet]
        [Route("properties/{idPattern}")]
        [RequiredRole(ClientRole.Read)]
        public QueuePropertiesResult QueueProperties(string idPattern)
        {
            try
            {
                if (_queues.AnyQueueExists(idPattern))
                {
                    bool forAccess = !this.Request.IsSlientAccess();

                    var queues = _queues.GetQueues(idPattern, forAccess)
                                        .OrderBy(q => q.Name);

                    var queuePropertiesResult = new QueuePropertiesResult(queues.CalcHashCode())
                    {
                        Queues = new Dictionary<string, Client.Models.QueueProperties>()
                    };

                    foreach (var queue in queues)
                    {
                        queuePropertiesResult.Queues[queue.Name] = new Client.Models.QueueProperties()
                        {
                            LastAccessUTC = queue.LastAccessUTC,
                            LastModifiedUTC = queue.LastModifiedUTC,

                            Length = queue.Where(item => item.IsValid(queue))
                                          .Count(),
                            UnconfirmedItems = _queues.UnconfirmedMessagesCount(queue, null),
                            DequeuingClientsCount = _queues.DequeuingClientsCount(queue),
                            DequeuingClients = _queues.UnconfirmedMessagesByClient(queue),

                            LifetimeSeconds = queue.Properties.LifetimeSeconds,
                            ItemLifetimeSeconds = queue.Properties.ItemLifetimeSeconds,

                            ConfirmationPeriodSeconds = queue.Properties.ConfirmationPeriodSeconds,
                            MaxUnconfirmedItems = queue.Properties.MaxUnconfirmedItems > 0 ? queue.Properties.MaxUnconfirmedItems : null,
                            MaxUnconfirmedItemsStrategy = queue.Properties.MaxUnconfirmedItemsStrategy,

                            SuspendDequeue = queue.Properties.SuspendDequeue,
                            SuspendEnqueue = queue.Properties.SuspendEnqueue,
                        };
                    }

                    return queuePropertiesResult;
                }
            }
            catch (Exception ex)
            {
                return new QueuePropertiesResult().AddExceptionMessage(ex, _logger);
            }


            return new QueuePropertiesResult(0);
        }

        [HttpGet]
        [Route("queuenames")]
        [RequiredRole(ClientRole.Read)]
        public QueueNamesResult QueueNames()
        {
            try
            {
                return new QueueNamesResult() { QueueNames = _queues.QueueNames };
            }
            catch (Exception ex)
            {
                return new QueueNamesResult().AddExceptionMessage(ex, _logger);
            }
        }
    }
}
