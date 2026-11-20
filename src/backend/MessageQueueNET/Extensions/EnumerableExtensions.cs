using MessageQueueNET.Models;
using MessageQueueNET.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageQueueNET.Extensions
{
    static public class EnumerableExtensions
    {
        static public Queue? QueueWithOldestDequeueAbleItem(this IEnumerable<Queue> queues,
                                                            QueuesService queueService,
                                                            string? clientId,
                                                            List<Queue> queueBag)
        {
            Queue? result = null;
            DateTime? oldestItemTime = null;
            bool querymManyQueues = queues.Count() > 1;

            foreach (var queue in queues)
            {
                if (queue.Properties.SuspendDequeue == true || queue.Count == 0)
                {
                    continue;
                }

                if (queue.Properties.MaxUnconfirmedItemsIsRestricted()
                    && queueService.UnconfirmedMessagesCount(
                          queue,
                          queue.Properties.MaxUnconfirmedItemsStrategy switch
                          {
                              Client.MaxUnconfirmedItemsStrategy.PerClient => clientId,
                              _ => null  // absolute
                          }) >= queue.Properties.MaxUnconfirmedItems)
                {
                    continue;
                }

                if (querymManyQueues && queueBag.Contains(queue)) // already took from here, take other if possible
                {
                    continue;
                }

                var oldestQueueItemTime = queue.Select(i => i.CreationDateUTC).Min();
                if (oldestItemTime == null || oldestQueueItemTime < oldestItemTime)
                {
                    oldestItemTime = oldestQueueItemTime;
                    result = queue;
                }
            }

            if (querymManyQueues)
            {
                if (result != null)
                {
                    queueBag.Add(result);
                }
                else if (result == null && queueBag.Count() > 0)
                {
                    queueBag.Clear();
                    return QueueWithOldestDequeueAbleItem(queues, queueService, clientId, queueBag);
                }
            }

            return result;
        }

        static public int CalcHashCode(this IEnumerable<Queue> queues)
        {
            int hashCode = 0;

            foreach (var queue in queues.OrderBy(q => q.Name))
            {
                hashCode = HashCode.Combine(hashCode, queue.LastModifiedUTC);
            }

            return hashCode;
        }

        // same calculation as for collections => hash codes from single queue results can be used for polling
        static public int CalcHashCode(this Queue queue) => new[] { queue }.CalcHashCode();
    }
}
