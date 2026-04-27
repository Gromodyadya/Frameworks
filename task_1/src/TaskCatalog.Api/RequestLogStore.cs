using System.Collections.Concurrent;

namespace TaskCatalog.Api;

public sealed class RequestLogStore
{
    // ConcurrentQueue выбрана потому, что записи в журнал могут добавляться из разных запросов.
    private readonly ConcurrentQueue<RequestLogEntry> _entries = new();

    public void Add(RequestLogEntry entry)
    {
        _entries.Enqueue(entry);

        // Ограничиваю журнал последними 200 записями, чтобы память не росла бесконечно.
        while (_entries.Count > 200 && _entries.TryDequeue(out _)) { }
    }

    // Возвращаю снимок журнала, чтобы внешний код не мог напрямую менять очередь.
    public IReadOnlyList<RequestLogEntry> List() => _entries.ToArray();
}
