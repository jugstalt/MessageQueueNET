namespace MessageQueueNET.Razor.Models;

public record ServerSummary(
    string Name,
    bool Loaded,
    int QueueCount,
    int Items,
    int Unconfirmed,
    int Problems,
    QueueStatus WorstStatus);
