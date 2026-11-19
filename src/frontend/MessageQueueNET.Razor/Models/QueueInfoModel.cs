using MessageQueueNET.Client;

namespace MessageQueueNET.Razor.Models;

public class QueueInfoModel
{
    public string QueueName { get; set; } = "";

    public DateTime LastAccessUTC { get; set; }
    public DateTime LastModifiedUTC { get; set; }

    public long LastAccessUtcTicks => LastAccessUTC.Ticks;
    public long LastModifiedUtcTicks => LastModifiedUTC.Ticks;

    public int QueueLength { get; set; }
    public int? UnconfirmedItems { get; set; }

    public bool SuspendEnqueue { get; set; }
    public bool SuspendDequeue { get; set; }

    public int LifetimeSeconds { get; set; }
    public int ItemLifetimeSeconds { get; set; }

    public int ConfirmationPeriodSeconds { get; set; }
    public int? MaxUnconfirmedItems { get; set; }
    public MaxUnconfirmedItemsStrategy MaxUnconfirmedItemsStrategy { get; set; }
    public IDictionary<string, int>? DequeuingClients { get; set; }
}
