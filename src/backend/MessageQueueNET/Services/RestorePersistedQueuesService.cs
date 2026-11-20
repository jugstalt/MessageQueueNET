using MessageQueueNET.Extensions;
﻿using MessageQueueNET.Models;
using MessageQueueNET.Services.Abstraction;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MessageQueueNET.Services
{
    public class RestorePersistedQueuesService
    {
        private readonly QueuesService _queues;
        private readonly IQueuesPersistService _persist;
        private readonly ILogger<RestorePersistedQueuesService> _logger;

        public RestorePersistedQueuesService(QueuesService queues,
                                             IQueuesPersistService persist,
                                             ILogger<RestorePersistedQueuesService> logger)
        {
            _queues = queues;
            _persist = persist;
            _logger = logger;
        }

        async public Task<bool> Restore()
        {
            bool success = true;

            foreach (var queueName in await _persist.QueueNames())
            {
                if (!queueName.IsValidQueueName())
                {
                    _logger.LogWarning("Skipping persisted queue {queueName}: invalid queue name (data is not deleted)", queueName);
                    continue;
                }

                try
                {
                    _queues.Restore(queueName,
                                    await _persist.GetQueueProperties(queueName) ?? new QueueProperties(),
                                    await _persist.GetAllItems(queueName),
                                    await _persist.GetAllUnconfirmedItems(queueName));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error restoring persisted queue {queueName}", queueName);
                    success = false;
                }
            }

            return success;
        }
    }
}
