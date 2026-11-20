using System.Net.Http.Json;
using System.Text.Json;

namespace MessageQueueNET.Tests;

public class QueueBehaviorTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static bool Success(JsonElement json) => json.GetProperty("success").GetBoolean();

    [Fact]
    public async Task Enqueue_RejectsTooLargeMessages()
    {
        using var factory = new MessageQueueFactory(new() { ["MessageQueue:Limits:MaxMessageBytes"] = "10" });
        var client = factory.CreateClient();

        Assert.True(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q", new[] { "small" }))));
        Assert.False(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q", new[] { new string('x', 11) }))));
    }

    [Fact]
    public async Task MaxQueues_IsEnforced()
    {
        using var factory = new MessageQueueFactory(new() { ["MessageQueue:Limits:MaxQueues"] = "2" });
        var client = factory.CreateClient();

        Assert.True(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q1", new[] { "m" }))));
        Assert.True(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q2", new[] { "m" }))));
        Assert.False(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q3", new[] { "m" }))));
    }

    [Fact]
    public async Task MaxQueueLength_IsEnforced()
    {
        using var factory = new MessageQueueFactory(new() { ["MessageQueue:Limits:MaxQueueLength"] = "2" });
        var client = factory.CreateClient();

        Assert.True(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q", new[] { "a", "b" }))));
        Assert.False(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q", new[] { "c" }))));
    }

    [Fact]
    public async Task Dequeue_ClampsCount()
    {
        using var factory = new MessageQueueFactory(new() { ["MessageQueue:Limits:MaxDequeueCount"] = "3" });
        var client = factory.CreateClient();

        await client.PutAsJsonAsync("/queue/enqueue/q", Enumerable.Range(0, 10).Select(i => i.ToString()));

        var result = await ReadJson(await client.PostAsync("/queue/dequeue/q?count=100", null));

        Assert.True(Success(result));
        Assert.Equal(3, result.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task EnqueueDequeueConfirm_Roundtrip()
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        Assert.True(Success(await ReadJson(await client.PutAsync("/queue/register/app.main.in?confirmationPeriodSeconds=60", null))));
        Assert.True(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/app.main.in", new[] { "hello" }))));
        Assert.True(Directory.Exists(Path.Combine(factory.PersistRoot, "app.main.in")));

        var dequeued = await ReadJson(await client.PostAsync("/queue/dequeue/app.main.in", null));
        var message = dequeued.GetProperty("messages")[0];
        Assert.Equal("hello", message.GetProperty("value").GetString());

        var messageId = message.GetProperty("id").GetString();
        Assert.True(Success(await ReadJson(await client.PostAsync($"/queue/confirmdequeue/app.main.in?messageId={messageId}", null))));
    }

    [Fact]
    public async Task ConfirmDequeue_OnlyByDequeuingUser_WhenAuthenticated()
    {
        using var factory = new MessageQueueFactory(MessageQueueFactory.BasicAuth(
            ("worker1", "s1", "write"), ("worker2", "s2", "write")));
        var worker1 = factory.CreateClient("worker1", "s1");
        var worker2 = factory.CreateClient("worker2", "s2");

        Assert.True(Success(await ReadJson(await worker1.PutAsync("/queue/register/q?confirmationPeriodSeconds=60", null))));
        await worker1.PutAsJsonAsync("/queue/enqueue/q", new[] { "hello" });

        var message = (await ReadJson(await worker1.PostAsync("/queue/dequeue/q", null))).GetProperty("messages")[0];
        var url = $"/queue/confirmdequeue/q?messageId={message.GetProperty("id").GetString()}";

        Assert.False(Success(await ReadJson(await worker2.PostAsync(url, null))));
        Assert.True(Success(await ReadJson(await worker1.PostAsync(url, null))));
    }

    [Fact]
    public async Task ConfirmDequeue_Roundtrip_WithoutAuth_StillWorks()
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        await client.PutAsync("/queue/register/q?confirmationPeriodSeconds=60", null);
        await client.PutAsJsonAsync("/queue/enqueue/q", new[] { "hello" });
        var message = (await ReadJson(await client.PostAsync("/queue/dequeue/q", null))).GetProperty("messages")[0];

        Assert.True(Success(await ReadJson(await client.PostAsync($"/queue/confirmdequeue/q?messageId={message.GetProperty("id").GetString()}", null))));
    }

    [Fact]
    public async Task ConfirmDequeue_DoesNotCreateQueue()
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        Assert.False(Success(await ReadJson(await client.PostAsync($"/queue/confirmdequeue/ghost?messageId={Guid.NewGuid()}", null))));

        var names = await ReadJson(await client.GetAsync("/queue/queuenames"));
        Assert.Equal(0, names.GetProperty("queueNames").GetArrayLength());
    }

    [Fact]
    public async Task Enqueue_ToSuspendedQueue_Fails()
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        await client.PutAsync("/queue/register/q?suspendEnqueue=true", null);

        Assert.False(Success(await ReadJson(await client.PutAsJsonAsync("/queue/enqueue/q", new[] { "m" }))));
    }
}
