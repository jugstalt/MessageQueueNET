using MessageQueueNET.Client;
using MessageQueueNET.Client.Models;
using MessageQueueNET.Client.Models.Authentication;
using MessageQueueNET.Models;
using MessageQueueNET.Services;
using MessageQueueNET.Services.Abstraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MessageQueueNET.Tests;

public class ReliabilityTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static bool Success(JsonElement json) => json.GetProperty("success").GetBoolean();

    // In memory persistence that can be told to fail
    private class FlakyPersist : QueuePersistNone, IQueuesPersistService
    {
        public int FailPersistItemFromCall { get; set; } = int.MaxValue;
        public bool FailRemoveItem { get; set; }
        public int PersistItemCalls;

        public new Task<bool> PersistQueueItem(string queueName, QueueItem item)
            => Task.FromResult(Interlocked.Increment(ref PersistItemCalls) < FailPersistItemFromCall);

        public new Task<bool> RemoveQueueItem(string queueName, Guid itemId)
            => Task.FromResult(!FailRemoveItem);
    }

    private static (HttpClient client, FlakyPersist persist, MessageQueueFactory factory) CreateFlaky()
    {
        var persist = new FlakyPersist();
        var factory = new MessageQueueFactory();
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IQueuesPersistService>(persist)))).CreateClient();

        return (client, persist, factory);
    }

    [Fact]
    public async Task Enqueue_IsAllOrNothing_WhenPersistFails()
    {
        var (client, persist, factory) = CreateFlaky();
        using var _ = factory;

        persist.FailPersistItemFromCall = 3;
        Assert.False(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q", new[] { "a", "b", "c", "d" }))));

        var length = await ReadJson(await client.GetAsync("/queue/length/q"));
        Assert.Equal(0, length.GetProperty("queues").GetProperty("q").GetProperty("queueLength").GetInt32());
    }

    [Fact]
    public async Task Dequeue_PutsMessageBack_WhenPersistFails()
    {
        var (client, persist, factory) = CreateFlaky();
        using var _ = factory;

        Assert.True(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q", new[] { "a" }))));

        persist.FailRemoveItem = true;
        Assert.False(Success(await ReadJson(await client.PostAsync("/queue/dequeue/q", null))));

        persist.FailRemoveItem = false;
        var dequeued = await ReadJson(await client.PostAsync("/queue/dequeue/q", null));
        Assert.Equal("a", dequeued.GetProperty("messages")[0].GetProperty("value").GetString());
    }

    [Fact]
    public void RemovedQueue_AcceptsNoItems_AndIsReplacedOnAccess()
    {
        var service = new QueuesService(Options.Create(new MessageQueueLimitsOptions()));
        var queue = service.GetQueue("q");

        Assert.True(queue.Enqueue(new QueueItem()));
        Assert.False(queue.TryMarkForRemoval(i => i.IsValid(queue)));  // has a valid item

        queue.Clear();
        Assert.True(queue.TryMarkForRemoval(i => i.IsValid(queue)));
        Assert.False(queue.Enqueue(new QueueItem()));

        var newQueue = service.GetQueue("q");
        Assert.NotSame(queue, newQueue);
        Assert.True(newQueue.Enqueue(new QueueItem()));

        // removing the old instance must not remove the new queue
        Assert.False(service.RemoveQueue(queue));
        Assert.True(service.QueueExists("q"));
    }

    [Fact]
    public void MaxQueues_IsNotExceeded_ByParallelRequests()
    {
        var service = new QueuesService(Options.Create(new MessageQueueLimitsOptions { MaxQueues = 5 }));

        Parallel.For(0, 200, i =>
        {
            try { service.GetQueue($"q{i}"); } catch (Exceptions.MessageQueueException) { }
        });

        Assert.Equal(5, service.QueueNames.Count());
    }

    [Fact]
    public async Task Client_UsesUtf8Credentials_ThrowsTypedExceptionAndEscapesQueueName()
    {
        using var factory = new MessageQueueFactory(MessageQueueFactory.BasicAuth(("üser", "pässwörd", null)));
        var http = factory.CreateClient();

        var connection = new MessageQueueConnection(http.BaseAddress!.ToString().TrimEnd('/'))
        {
            Authentication = new BasicAuthentication("üser", "pässwörd")
        };

        var client = new QueueClient(connection, "my.queue", http);
        await client.EnqueueAsync(new[] { "hello" });
        Assert.Equal("hello", (await client.DequeueAsync()).Messages!.Single().Value);

        var pattern = new QueueClient(connection, "my.*,other", http);
        Assert.Equal(0, (await pattern.LengthAsync()).Queues!["my.queue"].QueueLength);

        var wrong = new QueueClient(new MessageQueueConnection(connection.ApiUrl)
        {
            Authentication = new BasicAuthentication("üser", "wrong")
        }, "q", http);

        var ex = await Assert.ThrowsAsync<MessageQueueClientException>(() => wrong.LengthAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }
}
