namespace MeetingBooking.Api;

// Состояния процесса бронирования. Первые пять показывают нормальный путь,
// Failed оставлен как возможное состояние для расширения сценария.
public enum BookingState { New, RoomReserved, ParticipantsNotified, EquipmentPrepared, Completed, Failed }

// Это входное событие. В нем есть и ключ процесса, и ключ идемпотентности,
// потому что один процесс может состоять из нескольких разных событий.
public sealed record BookingEventRequest(
    string ProcessKey,
    string IdempotencyKey,
    string EventName,
    string CorrelationId,
    bool FailStep = false);

public sealed record BookingProcess(string ProcessKey, BookingState State, string? LastCorrelationId);

// Duplicate и Compensated вынесены в ответ, чтобы при ручной проверке сразу было видно,
// что произошло: обычный переход, повторная доставка или компенсация.
public sealed record BookingEventResult(
    string ProcessKey,
    BookingState State,
    bool Duplicate,
    bool Compensated,
    string CorrelationId,
    string Message);

// Журнальная запись хранит correlationId, чтобы можно было связать несколько сообщений
// одного запроса или эксперимента между собой.
public sealed record ProcessLogEntry(
    DateTimeOffset Time,
    string ProcessKey,
    string CorrelationId,
    string Action,
    string Message);

// Метрики специально простые: это не Prometheus, а учебный снимок счетчиков.
public sealed record MetricsSnapshot(
    long SuccessfulTransitions,
    long FailedTransitions,
    long DuplicateDeliveries,
    long Compensations,
    IReadOnlyDictionary<string, double> AverageLatencyMsByStep);
