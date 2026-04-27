namespace TaskCatalog.Api;

// Основная сущность предметной области: одна учебная задача в каталоге.
public sealed record StudyTask(
    int Id,
    string Title,
    string Course,
    int Difficulty,
    bool Completed,
    string? Notes,
    DateTimeOffset CreatedAt);

// Отдельная модель для POST-запроса. Id и CreatedAt клиент не присылает,
// потому что сервер сам выдает идентификатор и дату создания.
public sealed record CreateStudyTaskRequest(
    string? Title,
    string? Course,
    int Difficulty,
    bool Completed = false,
    string? Notes = null);

// В эту модель собираются query-параметры списка, чтобы не передавать их по одному дальше.
public sealed record TaskQuery(
    string? Course,
    bool? Completed,
    int? MinDifficulty,
    int? MaxDifficulty,
    string? Sort);

// Единая форма ошибки. RequestId нужен, чтобы найти этот же запрос в журнале.
public sealed record ErrorResponse(string ErrorCode, string Message, string RequestId);

// Запись внутреннего журнала: что пришло, чем ответили и сколько это заняло времени.
public sealed record RequestLogEntry(
    string RequestId,
    string Method,
    string Path,
    int StatusCode,
    double ElapsedMs,
    DateTimeOffset CreatedAt);
