namespace SecureTask.Api;

public sealed class RequestLogStore
{
    private readonly object _lock = new();
    private readonly Queue<RequestLog> _logs = new();

    public void Add(RequestLog log)
    {
        lock (_lock)
        {
            _logs.Enqueue(log);
            while (_logs.Count > 50) _logs.Dequeue();
        }
    }

    public RequestLog[] All() { lock (_lock) return [.. _logs]; }
}
