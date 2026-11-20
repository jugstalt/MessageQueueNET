using MessageQueueNET.Extensions;
using MessageQueueNET.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MessageQueueNET.Middleware;

/// <summary>
/// Long polling: waits until the hash code of the requested queues differs from the hash code sent by the client.
/// Must run after UseRouting and authentication.
/// </summary>
public class HashCodeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxPollingSeconds = 60;

    public HashCodeMiddleware(
                RequestDelegate next,
                IConfiguration configuration,
                ILogger<HashCodeMiddleware> logger
            )
    {
        _next = next;

        var maxPollingSeconds = configuration["MessageQueue:MaxRequestPollingSeconds"];
        if (!String.IsNullOrEmpty(maxPollingSeconds))
        {
            if (int.TryParse(maxPollingSeconds, out var value) && value >= 0)
            {
                _maxPollingSeconds = value;
            }
            else
            {
                logger.LogWarning("Invalid MessageQueue:MaxRequestPollingSeconds value {value}. Using default {default}", maxPollingSeconds, _maxPollingSeconds);
            }
        }

        logger.LogInformation("Max request polling duration set to {maxRequestPolling} seconds", _maxPollingSeconds);
    }

    public async Task InvokeAsync(HttpContext context,
                                  QueuesService queueService)
    {
        var idPattern = GetIdPattern(context);

        if (!String.IsNullOrEmpty(idPattern)
            && context.Request.TryGetHashCode(out var hashCode)
            && !String.IsNullOrEmpty(hashCode))
        {
            var stopwatch = Stopwatch.StartNew();
            int maxPollingSeconds = context.Request.MaxPollingSeconds(_maxPollingSeconds);
            bool forAccess = !context.Request.IsSlientAccess();

            try
            {
                while (stopwatch.Elapsed.TotalSeconds < maxPollingSeconds)
                {
                    // never creates queues => polling is free of side effects
                    var queues = queueService.GetQueues(idPattern, forAccess, createIfNotExists: false);

                    if (queues.CalcHashCode().ToString() != hashCode)
                    {
                        break;
                    }

                    await Task.Delay(100, context.RequestAborted);
                }
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return;  // client is gone
            }
        }

        await _next(context);
    }

    private static string? GetIdPattern(HttpContext context)
    {
        foreach (var key in new[] { "idPattern", "id" })
        {
            if (context.Request.RouteValues.TryGetValue(key, out var value) && value?.ToString() is string id)
            {
                return id.IsPattern() || id.IsValidQueueName() ? id : null;
            }
        }

        return null;
    }
}
