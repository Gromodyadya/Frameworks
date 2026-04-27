namespace MeetingBooking.Api;

public sealed class ProcessLogStore
{
    // Queue используется как простой кольцевой журнал: старые записи удаляются при переполнении.
    private readonly object _sync = new();
    private readonly Queue<ProcessLogEntry> _items = new();

    public void Add(string processKey, string correlationId, string action, string message)
    {
        lock (_sync)
        {
            _items.Enqueue(new(DateTimeOffset.UtcNow, processKey, correlationId, action, message));

            // Ограничение в 100 записей защищает учебный сервис от бесконечного роста памяти.
            while (_items.Count > 100) _items.Dequeue();
        }
    }

    public IReadOnlyList<ProcessLogEntry> All()
    {
        lock (_sync) return _items.ToArray();
    }
}
