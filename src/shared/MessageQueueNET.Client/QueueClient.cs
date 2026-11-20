using MessageQueueNET.Client.Abstractions;
using MessageQueueNET.Client.Extensions;
using MessageQueueNET.Client.Models;
using MessageQueueNET.Client.Models.Authentication;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MessageQueueNET.Client;

public class QueueClient : IQueueClient
{
    private static HttpClient ReuseableHttpClient = new HttpClient();
    private static string ClientId = $"{Environment.GetEnvironmentVariable("COMPUTERNAME")}-{Guid.NewGuid():N}";

    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _queueName;

    private readonly string _clientId, _clientSecret;
    private readonly JsonSerializerOptions _jsonOptions;

    public QueueClient(MessageQueueConnection connection, string queueName,
                       HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? ReuseableHttpClient;
        _apiUrl = connection.ApiUrl;
        _queueName = Uri.EscapeDataString(queueName);

        var uri = new Uri(_apiUrl);
        var userInfo = uri.UserInfo;

        string clientId = "", clientSecret = "";
        if (connection.Authentication is BasicAuthentication basicAuth)
        {
            clientId = basicAuth.Username;
            clientSecret = basicAuth.Password;
        }

        if (String.IsNullOrEmpty(clientId) &&
            String.IsNullOrEmpty(clientSecret) &&
            !String.IsNullOrEmpty(userInfo) && userInfo.Contains(":"))
        {
            _clientId = userInfo.Substring(0, userInfo.IndexOf(':'));
            _clientSecret = userInfo.Substring(userInfo.IndexOf(':') + 1);

            _apiUrl = _apiUrl.Replace($"{userInfo}@", "");
        }
        else
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    async public Task<InfoResult> ApiInfoAsync(CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_apiUrl}/info"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<InfoResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<MessagesResult> DequeueAsync(
        int count = 1,
        bool register = false,
        CancellationToken? cancelationToken = null,
        int? hashCode = null,
        int? maxPollingSeconds = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/queue/dequeue/{_queueName}?count={count}&register={register}"))
        {
            ModifyHttpRequest(requestMessage, hashCode, maxPollingSeconds);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<MessagesResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<ApiResult> ConfirmDequeueAsync(Guid messageId, CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/queue/confirmdequeue/{_queueName}?messageId={messageId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<ApiResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<ApiResult> EnqueueAsync(IEnumerable<string> messages, CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{_apiUrl}/queue/enqueue/{_queueName}"))
        {
            ModifyHttpRequest(requestMessage);

            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(messages),
                Encoding.UTF8,
                "application/json");

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<ApiResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<ApiResult> DeleteMessage(Guid messageId, CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{_apiUrl}/queue/deletemessage/{_queueName}?messageId={messageId}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<ApiResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<MessagesResult> AllMessagesAsync(int max = 0, bool unconfirmedOnly = false, CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_apiUrl}/queue/all/{_queueName}?max={max}&unconfirmedOnly={unconfirmedOnly}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<MessagesResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<QueueLengthResult> LengthAsync(CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_apiUrl}/queue/length/{_queueName}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<QueueLengthResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<ApiResult> RemoveAsync(RemoveType removeType = RemoveType.Queue, CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{_apiUrl}/queue/remove/{_queueName}?removeType={removeType}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<ApiResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<QueuePropertiesResult> RegisterAsync(int? lifetimeSeconds = null,
                                                           int? itemLifetimeSeconds = null,
                                                           int? confirmationPeriodSeconds = null,
                                                           int? maxUnconfirmedItems = null,
                                                           MaxUnconfirmedItemsStrategy? maxUnconfirmedItemsStrategy = null,
                                                           bool? suspendEnqueue = null,
                                                           bool? suspendDequeue = null,
                                                           CancellationToken? cancelationToken = null)
    {
        List<string> urlParameters = new List<string>();

        if (lifetimeSeconds.HasValue)
        {
            urlParameters.Add($"lifetimeSeconds={lifetimeSeconds.Value}");
        }
        if (itemLifetimeSeconds.HasValue)
        {
            urlParameters.Add($"itemLifetimeSeconds={itemLifetimeSeconds.Value}");
        }
        if (confirmationPeriodSeconds.HasValue)
        {
            urlParameters.Add($"confirmationPeriodSeconds={confirmationPeriodSeconds.Value}");
        }
        if (maxUnconfirmedItems.HasValue)
        {
            urlParameters.Add($"maxUnconfirmedItems={maxUnconfirmedItems.Value}");
        }
        if (maxUnconfirmedItemsStrategy.HasValue)
        {
            urlParameters.Add($"maxUnconfirmedItemsStrategy={maxUnconfirmedItemsStrategy.Value}");
        }
        if (suspendEnqueue.HasValue)
        {
            urlParameters.Add($"suspendEnqueue={suspendEnqueue.Value}");
        }
        if (suspendDequeue.HasValue)
        {
            urlParameters.Add($"suspendDequeue={suspendDequeue.Value}");
        }

        using (var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{_apiUrl}/queue/register/{_queueName}?{String.Join("&", urlParameters)}"))
        {
            ModifyHttpRequest(requestMessage);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<QueuePropertiesResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    async public Task<QueuePropertiesResult> PropertiesAsync(CancellationToken? cancelationToken = null,
                                                             int? hashCode = null,
                                                             int? maxPollingSeconds = null,
                                                             bool? silentAccess = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_apiUrl}/queue/properties/{_queueName}"))
        {
            ModifyHttpRequest(requestMessage, hashCode, maxPollingSeconds, silentAccess);

            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancellationToken: cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                string jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                return Result.Deserialize<QueuePropertiesResult>(jsonResponse, _jsonOptions);
            }
        }
    }

    async public Task<QueueNamesResult> QueueNamesAsync(CancellationToken? cancelationToken = null)
    {
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_apiUrl}/queue/queuenames"))
        {
            ModifyHttpRequest(requestMessage);
            using (var httpResponse = await _httpClient.SendAsync(requestMessage, cancelationToken ?? CancellationToken.None))
            {
                CheckHttpResponse(httpResponse);

                return Result.Deserialize<QueueNamesResult>(await httpResponse.Content.ReadAsStringAsync(), _jsonOptions);
            }
        }
    }

    static internal string ClientIdentity => ClientId;

    #region Helper

    private void ModifyHttpRequest(
                HttpRequestMessage requestMessage,
                int? hashCode = null,
                int? maxPollingSeconds = null,
                bool? silentAccess = null
           )
    {
        if (!String.IsNullOrEmpty(_clientId) && !String.IsNullOrEmpty(_clientSecret))
        {
            // Add Basic Auth
            var authenticationString = $"{_clientId}:{_clientSecret}";
            var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));

            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
        }

        if (hashCode.HasValue)
        {
            requestMessage.Headers.Add(MQHeaders.HashCode, hashCode.Value.ToString());
        }

        if (maxPollingSeconds.HasValue)
        {
            requestMessage.Headers.Add(MQHeaders.MaxPollingSeconds, maxPollingSeconds.Value.ToString());
        }

        if (silentAccess.HasValue)
        {
            requestMessage.Headers.Add(MQHeaders.SilentAccess, silentAccess.Value.ToString());
        }

        requestMessage.Headers.Add(MQHeaders.ClientId, ClientId);
        //Console.WriteLine($"Added Header {MQHeaders.ClientId}: {ClientId}");
    }

    private void CheckHttpResponse(HttpResponseMessage httpResponse)
    {
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new MessageQueueClientException(
                $"Error connecting with queue message service. Status code: {httpResponse.StatusCode}",
                httpResponse.StatusCode);
        }
    }

    #endregion
}
