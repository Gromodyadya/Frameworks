namespace TaskCatalog.Api;

public sealed record StudyTask(
    int Id,
    string Title,
    string Course,
    int Difficulty,
    bool Completed,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record CreateStudyTaskRequest(
    string? Title,
    string? Course,
    int Difficulty,
    bool Completed = false,
    string? Notes = null);

public sealed record TaskQuery(
    string? Course,
    bool? Completed,
    int? MinDifficulty,
    int? MaxDifficulty,
    string? Sort);

public sealed record ErrorResponse(string ErrorCode, string Message, string RequestId);

public sealed record RequestLogEntry(
    string RequestId,
    string Method,
    string Path,
    int StatusCode,
    double ElapsedMs,
    DateTimeOffset CreatedAt);
