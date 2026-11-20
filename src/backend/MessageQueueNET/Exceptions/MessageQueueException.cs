using System;

namespace MessageQueueNET.Exceptions;

/// <summary>
/// Expected errors (validation, suspended queues, limits).
/// Only the messages of these exceptions are returned to API clients.
/// </summary>
public class MessageQueueException : Exception
{
    public MessageQueueException(string message)
        : base(message)
    {
    }
}
