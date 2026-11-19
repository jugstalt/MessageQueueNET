namespace MessageQueueNET.Razor.Models;

// Ordered by severity (most severe first)
public enum QueueStatus
{
    Error = 0,   // enqueue or dequeue suspended
    Warn = 1,    // unconfirmed items reached the limit
    Idle = 2,    // items waiting, but nobody is dequeuing
    Ok = 3,
    Empty = 4
}
