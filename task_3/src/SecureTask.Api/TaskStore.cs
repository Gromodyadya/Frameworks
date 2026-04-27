namespace SecureTask.Api;

public sealed class TaskStore
{
    private readonly object _lock = new();
    private readonly List<TaskItem> _items = [new(1, "Check secure startup", false, DateTimeOffset.UtcNow)];
    private int _nextId = 2;

    public TaskItem[] All() { lock (_lock) return [.. _items.OrderBy(i => i.Id)]; }

    public TaskItem Add(CreateTaskRequest request)
    {
        var title = request.Title?.Trim();
        // Простая валидация нужна, чтобы в хранилище не попадали пустые или слишком длинные строки.
        if (string.IsNullOrWhiteSpace(title) || title.Length > 80)
            throw new AppException(400, "VALIDATION_ERROR", "Title is required and must be 1-80 chars.");
        lock (_lock)
        {
            // lock защищает счетчик id, если несколько запросов придут одновременно.
            var item = new TaskItem(_nextId++, title, false, DateTimeOffset.UtcNow);
            _items.Add(item);
            return item;
        }
    }
}
