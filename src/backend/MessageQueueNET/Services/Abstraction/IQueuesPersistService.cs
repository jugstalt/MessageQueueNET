﻿using MessageQueueNET.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MessageQueueNET.Services.Abstraction
{
    public interface IQueuesPersistService
    {
        Task<bool> PersistQueueProperties(Queue queue);

        Task<QueueProperties?> GetQueueProperties(string queueName);

        Task<bool> PersistQueueItem(string queueName, QueueItem item);

        Task<bool> RemoveQueueItem(string queueName, Guid itemId);

        Task<IEnumerable<QueueItem>> GetAllItems(string queueName);

        Task<bool> PersistUnconfirmedQueueItem(string queueName, QueueItem item);
        Task<bool> RemoveUnconfirmedQueueItem(string queueName, Guid itemId);
        Task<IEnumerable<QueueItem>> GetAllUnconfirmedItems(string queueName);


        Task<bool> RemoveQueue(string queueName);
        Task<bool> RemoveQueueMessages(string queueName);
        Task<bool> RemoveQueueUnconfirmedMessages(string queueName);

        Task<IEnumerable<string>> QueueNames();
    }
}
