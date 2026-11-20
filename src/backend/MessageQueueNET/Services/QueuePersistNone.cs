using MessageQueueNET.Models;
using MessageQueueNET.Services.Abstraction;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MessageQueueNET.Services
{
    public class QueuePersistNone : IQueuesPersistService
    {
        public Task<IEnumerable<QueueItem>> GetAllItems(string queueName)
            => Task.FromResult<IEnumerable<QueueItem>>(new QueueItem[0]);
        public Task<IEnumerable<QueueItem>> GetAllUnconfirmedItems(string queueName)
            => Task.FromResult<IEnumerable<QueueItem>>(new QueueItem[0]);

        public Task<QueueProperties?> GetQueueProperties(string queueName)
            => Task.FromResult<QueueProperties?>(null);

        public Task<bool> PersistQueueItem(string queueName, QueueItem item)
            => Task.FromResult(true);

        public Task<bool> PersistUnconfirmedQueueItem(string queueName, QueueItem item)
            => Task.FromResult(true);
        public Task<bool> RemoveUnconfirmedQueueItem(string queueName, Guid itemId)
            => Task.FromResult(true);

        public Task<bool> PersistQueueProperties(Queue queue)
            => Task.FromResult(true);

        public Task<IEnumerable<string>> QueueNames()
            => Task.FromResult<IEnumerable<string>>(new string[0]);

        public Task<bool> RemoveQueue(string queueName)
            => Task.FromResult(true);
        public Task<bool> RemoveQueueMessages(string queueName)
            => Task.FromResult(true);
        public Task<bool> RemoveQueueUnconfirmedMessages(string queueName)
            => Task.FromResult(true);

        public Task<bool> RemoveQueueItem(string queueName, Guid itemId)
            => Task.FromResult(true);

    }
}
