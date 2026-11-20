using System;
using System.Net;

namespace MessageQueueNET.Client;

/// <summary>
/// The message queue service answered with a non-success HTTP status code
/// (eg. 401 unauthorized, 403 forbidden, 500 internal server error).
/// </summary>
public class MessageQueueClientException : Exception
{
    public MessageQueueClientException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
