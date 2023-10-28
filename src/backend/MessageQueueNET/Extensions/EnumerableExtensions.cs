﻿using MessageQueueNET.Models;
using MessageQueueNET.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageQueueNET.Extensions
{
    static public class EnumerableExtensions
    {
        static public Queue? QueueWithOldestDequeueAbleItem(this IEnumerable<Queue> queues,
                                                            QueuesService queueService)
        {
            Queue? result = null;
            DateTime? oldestItemTime = null;

            foreach (var queue in queues)
            {
                if (queue.Properties.SuspendDequeue == true || queue.Count() == 0)
                {
                    continue;
                }

                if (queue.Properties.MaxUnconfiredItems > 0
                    && queueService.UnconfirmedMessagesCount(queue) >= queue.Properties.MaxUnconfiredItems)
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

            return result;
        }

        static public int CalcHashCode(this IEnumerable<Queue> queues)
        {
            int hashCode = 0;

            foreach (var queue in queues.OrderBy(q=>q.Name))
            {
                hashCode = HashCode.Combine(hashCode, queue.LastModifiedUTC);
            }

            return hashCode;
        }

        static public int CalcHashCode(this Queue queue) => queue.LastAccessUTC.GetHashCode();
    }
}
