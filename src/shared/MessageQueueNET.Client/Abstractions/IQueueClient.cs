using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MessageQueueNET.Client.Models;

namespace MessageQueueNET.Client.Abstractions
{
    public interface IQueueClient
    {
        Task<MessagesResult> AllMessagesAsync(int max = 0, bool unconfirmedOnly = false, CancellationToken? cancelationToken = null);

        Task<InfoResult> ApiInfoAsync(CancellationToken? cancelationToken = null);

        Task<ApiResult> ConfirmDequeueAsync(Guid messageId, CancellationToken? cancelationToken = null);

        Task<ApiResult> DeleteMessage(Guid messageId, CancellationToken? cancelationToken = null);

        Task<MessagesResult> DequeueAsync(
            int count = 1,
            bool register = false,
            CancellationToken? cancelationToken = null,
            int? hashCode = null,
            int? maxPollingSeconds = null
        );

        Task<ApiResult> EnqueueAsync(IEnumerable<string> messages, CancellationToken? cancelationToken = null);

        Task<QueueLengthResult> LengthAsync(CancellationToken? cancelationToken = null);

        Task<QueuePropertiesResult> PropertiesAsync(
            CancellationToken? cancelationToken = null,
            int? hashCode = null,
            int? maxPollingSeconds = null,
            bool? silentAccess = null
        );

        Task<QueueNamesResult> QueueNamesAsync(CancellationToken? cancelationToken = null);

        Task<QueuePropertiesResult> RegisterAsync(
            int? lifetimeSeconds = null,
            int? itemLifetimeSeconds = null,
            int? confirmationPeriodSeconds = null,
            int? maxUnconfirmedItems = null,
            MaxUnconfirmedItemsStrategy? maxUnconfirmedItemsStrategy = null,
            bool? suspendEnqueue = null,
            bool? suspendDequeue = null,
            CancellationToken? cancelationToken = null
        );

        Task<ApiResult> RemoveAsync(RemoveType removeType = RemoveType.Queue, CancellationToken? cancelationToken = null);
    }
}
