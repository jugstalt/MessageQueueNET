using MessageQueueNET.Client;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MessageQueueNET.Tests;

public class SecurityTests
{
    private static MessageQueueFactory CreateWithAuth()
    {
        var sha256Secret = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hashed-secret")));

        return new MessageQueueFactory(MessageQueueFactory.BasicAuth(
            ("admin", "admin-secret", null),
            ("reader", "reader-secret", "read"),
            ("writer", "writer-secret", "write"),
            ("hashed", sha256Secret, "admin")));
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task AssertApiError(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await ReadJson(response)).GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("%2E%2E")]
    [InlineData("..%2F..%2Fevil")]
    public async Task EnqueueWithTraversalName_IsRejected_AndWritesNothingOutsidePersistRoot(string name)
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/queue/enqueue/{name}", new[] { "payload" });

        // either rejected by routing (url normalization) or by the api
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.False((await ReadJson(response)).GetProperty("success").GetBoolean());
        }

        Assert.Empty(Directory.GetFiles(factory.TempRoot, "*.json", SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.GetFileSystemEntries(factory.PersistRoot));
    }

    [Fact]
    public async Task UnauthenticatedLongPolling_Returns401Immediately_AndCreatesNoQueue()
    {
        using var factory = CreateWithAuth();
        var anonymous = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/queue/length/newqueue");
        request.Headers.Add(MQHeaders.HashCode, "12345");

        var stopwatch = Stopwatch.StartNew();
        var response = await anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));

        var names = await ReadJson(await factory.CreateClient("admin", "admin-secret").GetAsync("/queue/queuenames"));
        Assert.Equal(0, names.GetProperty("queueNames").GetArrayLength());
    }

    [Theory]
    [InlineData("Basic not-base64!!!")]
    [InlineData("Basic Og==")]            // ":"
    [InlineData("Bearer abc")]
    public async Task InvalidAuthorizationHeader_Returns401(string header)
    {
        using var factory = CreateWithAuth();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);

        var response = await client.GetAsync("/queue/queuenames");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("admin", "wrong", HttpStatusCode.Unauthorized)]
    [InlineData("unknown", "admin-secret", HttpStatusCode.Unauthorized)]
    [InlineData("admin", "admin-secret", HttpStatusCode.OK)]
    [InlineData("ADMIN", "admin-secret", HttpStatusCode.OK)]
    [InlineData("hashed", "hashed-secret", HttpStatusCode.OK)]
    [InlineData("hashed", "sha256:wrong", HttpStatusCode.Unauthorized)]
    public async Task Credentials(string user, string password, HttpStatusCode expected)
    {
        using var factory = CreateWithAuth();

        var response = await factory.CreateClient(user, password).GetAsync("/queue/queuenames");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerAndInfo_RequireAuthentication()
    {
        using var factory = CreateWithAuth();
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/info")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/swagger/v2/swagger.json")).StatusCode);
    }

    [Fact]
    public async Task Roles_AreEnforced()
    {
        using var factory = CreateWithAuth();
        var reader = factory.CreateClient("reader", "reader-secret");
        var writer = factory.CreateClient("writer", "writer-secret");
        var admin = factory.CreateClient("admin", "admin-secret");

        // read
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/queue/queuenames")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync("/queue/enqueue/q1", new[] { "m" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync("/queue/dequeue/q1", null)).StatusCode);

        // write
        Assert.Equal(HttpStatusCode.OK, (await writer.PutAsJsonAsync("/queue/enqueue/q1", new[] { "m" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await writer.PostAsync("/queue/dequeue/q1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await writer.PutAsync("/queue/register/q2", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.PutAsync("/queue/register/q*", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.DeleteAsync("/queue/remove/q*")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.DeleteAsync($"/queue/deletemessage/q1?messageId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await writer.DeleteAsync("/queue/remove/q2")).StatusCode);

        // admin
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync("/queue/remove/q*")).StatusCode);
    }

    [Fact]
    public async Task MutatingEndpoints_RejectGet()
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        foreach (var path in new[] { "/queue/remove/q1", "/queue/dequeue/q1", "/queue/register/q1", "/queue/confirmdequeue/q1", "/queue/deletemessage/q1" })
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task LongPolling_DoesNotCreateQueues()
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/queue/length/polled");
        request.Headers.Add(MQHeaders.HashCode, "12345");
        await client.SendAsync(request);

        var names = await ReadJson(await client.GetAsync("/queue/queuenames"));
        Assert.DoesNotContain(names.GetProperty("queueNames").EnumerateArray(), n => n.GetString() == "polled");
    }

    [Fact]
    public async Task InvalidQueueName_ReturnsApiError_WithoutTouchingFilesystem()
    {
        using var factory = new MessageQueueFactory();
        var client = factory.CreateClient();

        await AssertApiError(await client.PutAsJsonAsync("/queue/enqueue/a..b", new[] { "m" }));
        await AssertApiError(await client.PutAsync("/queue/register/_x", null));

        Assert.Empty(Directory.GetFileSystemEntries(factory.PersistRoot));
    }
}
