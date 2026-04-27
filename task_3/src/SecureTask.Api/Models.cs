namespace SecureTask.Api;

public sealed record TaskItem(int Id, string Title, bool Completed, DateTimeOffset CreatedAt);
public sealed record CreateTaskRequest(string? Title);
public sealed record ErrorResponse(string ErrorCode, string Message, string RequestId);
public sealed record RequestLog(DateTimeOffset At, string Method, string Path, int StatusCode, string Source);
