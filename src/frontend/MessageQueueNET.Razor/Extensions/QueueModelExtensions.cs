using MessageQueueNET.Client.Models;
using MessageQueueNET.Razor.Models;

namespace MessageQueueNET.Razor.Extensions;

static internal class QueueModelExtensions
{
    public static IEnumerable<QueueInfoModel> ToQueueInfoModels(this QueuePropertiesResult queuePropertiesResult)
        => queuePropertiesResult.Queues?.Keys.Select(q => new QueueInfoModel()
        {
            QueueName = q,

            LastAccessUTC = queuePropertiesResult.Queues[q].LastAccessUTC,
            LastModifiedUTC = queuePropertiesResult.Queues[q].LastModifiedUTC,

            QueueLength = queuePropertiesResult.Queues[q].Length,
            UnconfirmedItems = queuePropertiesResult.Queues[q].UnconfirmedItems,

            SuspendEnqueue = queuePropertiesResult.Queues[q].SuspendEnqueue,
            SuspendDequeue = queuePropertiesResult.Queues[q].SuspendDequeue,

            LifetimeSeconds = queuePropertiesResult.Queues[q].LifetimeSeconds,
            ItemLifetimeSeconds = queuePropertiesResult.Queues[q].ItemLifetimeSeconds,

            ConfirmationPeriodSeconds = queuePropertiesResult.Queues[q].ConfirmationPeriodSeconds,
            MaxUnconfirmedItems = queuePropertiesResult.Queues[q].MaxUnconfirmedItems,
            MaxUnconfirmedItemsStrategy = queuePropertiesResult.Queues[q].MaxUnconfirmedItemsStrategy,
            DequeuingClients = queuePropertiesResult.Queues[q].DequeuingClients
        }) ?? Array.Empty<QueueInfoModel>();

    public static IEnumerable<QueueInfoModel> FitsSearchString(this IEnumerable<QueueInfoModel> items, string searchString)
        => string.IsNullOrEmpty(searchString)
        ? items
        : items.Where(i => i.QueueName.Contains(searchString, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<MessageResult> FitsSearchString(this IEnumerable<MessageResult> items, string searchString)
        => string.IsNullOrEmpty(searchString)
        ? items
        : items.Where(i => i.Value?.Contains(searchString, StringComparison.OrdinalIgnoreCase) == true);
}
