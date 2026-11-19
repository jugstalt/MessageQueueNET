using MessageQueueNET.Client;

namespace MessageQueueNET.Razor.Models;

public record QueueDeletion(IReadOnlyList<string> QueueNames, RemoveType RemoveType);
