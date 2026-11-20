using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Text;

namespace MessageQueueNET.Tests;

/// <summary>
/// Hosts the message queue in memory. Every instance uses its own temp folder as content root and persist root,
/// so the repository's _config/message-queue.json is never loaded.
/// </summary>
public class MessageQueueFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings;

    public MessageQueueFactory(Dictionary<string, string?>? settings = null)
    {
        TempRoot = Path.Combine(Path.GetTempPath(), "mq-tests", Guid.NewGuid().ToString("N"));
        PersistRoot = Path.Combine(TempRoot, "persist");
        Directory.CreateDirectory(PersistRoot);

        _settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["MessageQueue:Persist:Type"] = "filesystem",
            ["MessageQueue:Persist:RootPath"] = PersistRoot,
            ["MessageQueue:MaxRequestPollingSeconds"] = "2"
        };

        foreach (var setting in settings ?? [])
        {
            _settings[setting.Key] = setting.Value;
        }
    }

    public string TempRoot { get; }
    public string PersistRoot { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(TempRoot);

        foreach (var setting in _settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }
    }

    public HttpClient CreateClient(string user, string password)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            Directory.Delete(TempRoot, true);
        }
        catch { }
    }

    public static Dictionary<string, string?> BasicAuth(params (string id, string secret, string? role)[] clients)
    {
        var settings = new Dictionary<string, string?> { ["Authorization:Type"] = "Basic" };

        for (int i = 0; i < clients.Length; i++)
        {
            settings[$"Authorization:Clients:{i}:Id"] = clients[i].id;
            settings[$"Authorization:Clients:{i}:Secret"] = clients[i].secret;
            if (clients[i].role is not null)
            {
                settings[$"Authorization:Clients:{i}:Roles:0"] = clients[i].role;
            }
        }

        return settings;
    }
}
