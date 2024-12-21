using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MessageQueueNET.Client.Models;

namespace MessageQueueNET.Client.Abstractions
{
    public interface IQueueClient
    {
        Task<MessagesResult> AllMessagesAsync(int max = 0, bool unconfirmedOnly = false);

        Task<InfoResult> ApiInfoAsync();

        Task<ApiResult> ConfirmDequeueAsync(Guid messageId);

        Task<ApiResult> DeleteMessage(Guid messageId);

        Task<MessagesResult> DequeueAsync(
            int count = 1,
            bool register = false,
            CancellationToken? cancelationToken = null,
            int? hashCode = null,
            int? maxPollingSeconds = null
        );

        Task<ApiResult> EnqueueAsync(IEnumerable<string> messages);

        Task<QueueLengthResult> LengthAsync();

        Task<QueuePropertiesResult> PropertiesAsync(
            CancellationToken? cancelationToken = null,
            int? hashCode = null,
            int? maxPollingSeconds = null,
            bool? silentAccess = null
        );

        Task<QueueNamesResult> QueueNamesAsync();

        Task<QueuePropertiesResult> RegisterAsync(
            int? lifetimeSeconds = null,
            int? itemLifetimeSeconds = null,
            int? confirmationPeriodSeconds = null,
            int? maxUnconfirmedItems = null,
            MaxUnconfirmedItemsStrategy? maxUnconfirmedItemsStrategy = null,
            bool? suspendEnqueue = null,
            bool? suspendDequeue = null
        );

        Task<ApiResult> RemoveAsync(RemoveType removeType = RemoveType.Queue);
    }
}
