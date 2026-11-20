using MessageQueueNET.Services.Abstraction;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MessageQueueNET.Services
{
    public class TimedHostedBackgroundService : BackgroundService
    {
        private readonly QueuesService _queues;
        private readonly IQueuesPersistService _persist;
        private readonly ILogger<TimedHostedBackgroundService> _logger;

        public TimedHostedBackgroundService(QueuesService queues,
                                            IQueuesPersistService persist,
                                            ILogger<TimedHostedBackgroundService> logger)
        {
            _queues = queues;
            _persist = persist;
            _logger = logger;
        }

        async protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await DoWork();

                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        async private Task DoWork()
        {
            try
            {
                foreach (var queue in _queues.Queues)
                {
                    if (queue.Properties.ConfirmationPeriodSeconds > 0)
                    {
                        await _queues.ReEnqueueUnconfirmedMessages(queue, _persist);
                    }

                    if (queue.Properties.LifetimeSeconds > 0 &&
                        (DateTime.UtcNow - queue.LastAccessUTC).TotalSeconds > queue.Properties.LifetimeSeconds)
                    {
                        var itemCount = queue.Where(item => item.IsValid(queue)).Count() +
                                        (_queues.UnconfirmedMessagesCount(queue, null) ?? 0);
                        if (itemCount == 0)
                        {
                            if (await _persist.RemoveQueue(queue.Name))
                            {
                                _queues.RemoveQueue(queue.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue maintenance");
            }
        }
    }
}
