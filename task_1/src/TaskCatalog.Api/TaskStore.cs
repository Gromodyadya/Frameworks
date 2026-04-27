namespace TaskCatalog.Api;

public sealed class TaskStore
{
    private readonly Lock _lock = new();
    private readonly List<StudyTask> _items = [];
    private readonly TaskValidator _validator;
    private int _nextId = 1;

    public TaskStore(TaskValidator validator)
    {
        _validator = validator;
        Create(new("Build minimal API", "Frameworks", 3, false, "Seed item"));
        Create(new("Write lab report", "Frameworks", 2, true, "Seed item"));
    }

    public IReadOnlyList<StudyTask> List(TaskQuery query)
    {
        lock (_lock)
        {
            var items = _items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.Course))
                items = items.Where(x => x.Course.Contains(query.Course, StringComparison.OrdinalIgnoreCase));
            if (query.Completed is not null)
                items = items.Where(x => x.Completed == query.Completed);
            if (query.MinDifficulty is not null)
                items = items.Where(x => x.Difficulty >= query.MinDifficulty);
            if (query.MaxDifficulty is not null)
                items = items.Where(x => x.Difficulty <= query.MaxDifficulty);
            return Sort(items, query.Sort).ToArray();
        }
    }

    public StudyTask Get(int id)
    {
        lock (_lock)
            return _items.FirstOrDefault(x => x.Id == id)
                ?? throw new AppException(StatusCodes.Status404NotFound, "ITEM_NOT_FOUND", $"Item {id} was not found.");
    }

    public StudyTask Create(CreateStudyTaskRequest request)
    {
        _validator.Validate(request);
        lock (_lock)
        {
            var item = new StudyTask(_nextId++, request.Title!.Trim(), request.Course!.Trim(),
                request.Difficulty, request.Completed, request.Notes?.Trim(), DateTimeOffset.UtcNow);
            _items.Add(item);
            return item;
        }
    }

    private static IOrderedEnumerable<StudyTask> Sort(IEnumerable<StudyTask> items, string? sort) => sort?.ToLowerInvariant() switch
    {
        "title" => items.OrderBy(x => x.Title),
        "-title" => items.OrderByDescending(x => x.Title),
        "difficulty" => items.OrderBy(x => x.Difficulty),
        "-difficulty" => items.OrderByDescending(x => x.Difficulty),
        "-created" => items.OrderByDescending(x => x.CreatedAt),
        _ => items.OrderBy(x => x.CreatedAt)
    };
}
