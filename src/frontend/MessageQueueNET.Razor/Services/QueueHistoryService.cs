namespace MessageQueueNET.Razor.Services;

/// <summary>
/// Client-side (per circuit) history of queue lengths, used for sparklines and charts.
/// Samples the latest known queue properties in a fixed interval, because the
/// long-polling results only arrive when something changes.
/// </summary>
internal sealed class QueueHistoryService : IDisposable
{
    public const int Capacity = 100;
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(3);
    public static TimeSpan Window => SampleInterval * Capacity;

    private readonly QueryQueueEventBus _queryEvents;
    private readonly Timer _timer;
    private readonly object _lock = new();
    private readonly Dictionary<string, Queue<int>> _queueHistory = new();
    private readonly Dictionary<string, Queue<int>> _serverHistory = new();

    public QueueHistoryService(QueryQueueEventBus queryEvents)
    {
        _queryEvents = queryEvents;
        _timer = new Timer(_ => Sample(), null, TimeSpan.Zero, SampleInterval);
    }

    public event Func<Task>? OnSampledAsync;

    public IReadOnlyList<int> QueueHistory(string serverName, string queueName)
    {
        lock (_lock)
        {
            return _queueHistory.TryGetValue(Key(serverName, queueName), out var history)
                ? history.ToArray()
                : Array.Empty<int>();
        }
    }

    public IReadOnlyList<int> ServerHistory(string serverName)
    {
        lock (_lock)
        {
            return _serverHistory.TryGetValue(serverName, out var history)
                ? history.ToArray()
                : Array.Empty<int>();
        }
    }

    private void Sample()
    {
        bool sampled = false;

        lock (_lock)
        {
            var activeKeys = new HashSet<string>();

            foreach (var (serverName, result) in _queryEvents.QueuePropertiesResult)
            {
                var queues = result.Queues ?? new();

                Append(_serverHistory, serverName, queues.Values.Sum(q => q.Length));

                foreach (var (queueName, properties) in queues)
                {
                    var key = Key(serverName, queueName);
                    activeKeys.Add(key);
                    Append(_queueHistory, key, properties.Length);
                }

                sampled = true;
            }

            foreach (var removedKey in _queueHistory.Keys.Where(k => !activeKeys.Contains(k)).ToArray())
            {
                _queueHistory.Remove(removedKey);
            }
        }

        if (sampled)
        {
            _ = OnSampledAsync?.Invoke();
        }
    }

    private static void Append(Dictionary<string, Queue<int>> store, string key, int value)
    {
        if (!store.TryGetValue(key, out var history))
        {
            store[key] = history = new Queue<int>(Capacity);
        }

        history.Enqueue(value);
        while (history.Count > Capacity)
        {
            history.Dequeue();
        }
    }

    private static string Key(string serverName, string queueName) => $"{serverName}\n{queueName}";

    public void Dispose() => _timer.Dispose();
}
