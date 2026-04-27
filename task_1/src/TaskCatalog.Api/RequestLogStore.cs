using System.Collections.Concurrent;

namespace TaskCatalog.Api;

public sealed class RequestLogStore
{
    private readonly ConcurrentQueue<RequestLogEntry> _entries = new();

    public void Add(RequestLogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > 200 && _entries.TryDequeue(out _)) { }
    }

    public IReadOnlyList<RequestLogEntry> List() => _entries.ToArray();
}
