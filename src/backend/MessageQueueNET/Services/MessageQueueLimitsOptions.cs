namespace MessageQueueNET.Services;

/// <summary>
/// Configuration section: MessageQueue:Limits
/// Values &lt;= 0 mean "unlimited".
/// </summary>
public class MessageQueueLimitsOptions
{
    public int MaxQueues { get; set; } = 10_000;
    public int MaxMessageBytes { get; set; } = 1024 * 1024;
    public int MaxDequeueCount { get; set; } = 1_000;
    public int MaxQueueLength { get; set; } = 0;
}
