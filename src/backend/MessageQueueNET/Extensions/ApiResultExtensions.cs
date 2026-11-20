using MessageQueueNET.Client.Models;
using MessageQueueNET.Exceptions;
using Microsoft.Extensions.Logging;
using System;

namespace MessageQueueNET.Extensions;

internal static class ApiResultExtensions
{
    public const string InternalErrorMessage = "Internal server error";

    static public T AddExceptionMessage<T>(this T apiResult, Exception ex, ILogger logger)
        where T : ApiResult
    {
        apiResult.Success = false;

        if (ex is MessageQueueException)
        {
            apiResult.ErrorMessage = ex.Message;
        }
        else
        {
            logger.LogError(ex, "Unexpected error");
            apiResult.ErrorMessage = InternalErrorMessage;
        }

        return apiResult;
    }
}
