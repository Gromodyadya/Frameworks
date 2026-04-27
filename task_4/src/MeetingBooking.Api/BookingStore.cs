using System.Diagnostics;

namespace MeetingBooking.Api;

public sealed class BookingStore(ProcessLogStore logs, MetricsStore metrics, ReadinessState readiness)
{
    // Один lock закрывает и состояние процесса, и список обработанных ключей.
    // Так проще объяснить потокобезопасность: два одинаковых события не пройдут одновременно.
    private readonly object _sync = new();
    private readonly Dictionary<string, ProcessData> _processes = new(StringComparer.OrdinalIgnoreCase);

    public BookingProcess Get(string processKey)
    {
        lock (_sync)
        {
            return _processes.TryGetValue(processKey, out var data)
                ? new(data.ProcessKey, data.State, data.LastCorrelationId)
                : throw new AppException(StatusCodes.Status404NotFound, "PROCESS_NOT_FOUND", $"Process '{processKey}' was not found.");
        }
    }

    public BookingEventResult Apply(BookingEventRequest request)
    {
        Validate(request);

        // Таймер нужен для грубой оценки задержки конкретного шага в метриках.
        var timer = Stopwatch.StartNew();
        lock (_sync)
        {
            var data = GetOrCreate(request.ProcessKey);

            // Главная проверка идемпотентности: если такой ключ уже был, состояние не трогаем.
            if (data.Results.TryGetValue(request.IdempotencyKey, out var saved))
            {
                metrics.Duplicate();
                logs.Add(data.ProcessKey, request.CorrelationId, "duplicate", $"Event '{request.EventName}' was already delivered.");

                // CorrelationId берем из нового запроса, чтобы повтор тоже было видно в журнале и ответе.
                return saved with { Duplicate = true, CorrelationId = request.CorrelationId };
            }

            try
            {
                var result = Move(data, request, timer);

                // Результат сохраняется после успешной обработки. При повторной доставке вернем его же.
                data.Results[request.IdempotencyKey] = result;
                return result;
            }
            catch
            {
                metrics.Failure();
                throw;
            }
        }
    }

    private BookingEventResult Move(ProcessData data, BookingEventRequest request, Stopwatch timer)
    {
        var step = request.EventName.Trim();

        // failStep нужен только для учебной демонстрации частичного сбоя следующего шага.
        if (request.FailStep && step.Equals("PrepareEquipment", StringComparison.OrdinalIgnoreCase))
            return CompensateAfterEquipmentFailure(data, request);

        // Здесь описана сама машина состояний: разрешены только конкретные пары "состояние + событие".
        var next = (data.State, step) switch
        {
            (BookingState.New, "ReserveRoom") => BookingState.RoomReserved,
            (BookingState.RoomReserved, "NotifyParticipants") => BookingState.ParticipantsNotified,
            (BookingState.ParticipantsNotified, "PrepareEquipment") => BookingState.EquipmentPrepared,
            (BookingState.EquipmentPrepared, "ConfirmBooking") => BookingState.Completed,
            _ => throw new AppException(StatusCodes.Status409Conflict, "INVALID_TRANSITION",
                $"Event '{step}' cannot be applied to state '{data.State}'.")
        };

        // Если переход разрешен, меняем состояние и сразу фиксируем это в метриках и журнале.
        data.State = next;
        data.LastCorrelationId = request.CorrelationId;
        metrics.Success(step, timer.ElapsedMilliseconds);
        logs.Add(data.ProcessKey, request.CorrelationId, "transition", $"{step}: state changed to {next}.");
        return new(data.ProcessKey, data.State, false, false, request.CorrelationId, "Transition applied.");
    }

    private BookingEventResult CompensateAfterEquipmentFailure(ProcessData data, BookingEventRequest request)
    {
        // Сбой оборудования имеет смысл только после уведомления участников.
        // В другом состоянии это уже ошибка сценария, а не компенсация.
        if (data.State != BookingState.ParticipantsNotified)
            throw new AppException(StatusCodes.Status409Conflict, "INVALID_FAILURE_POINT", "Equipment failure is allowed only after participant notification.");

        // Здесь специально откатываю предыдущий успешный шаг: приглашения считаем отмененными.
        // Поэтому состояние возвращается не в New, а в RoomReserved: комната еще удерживается.
        data.State = BookingState.RoomReserved;
        data.LastCorrelationId = request.CorrelationId;

        // Компенсируемый сбой считается и ошибочным переходом, и отдельной компенсацией.
        metrics.Failure();
        metrics.Compensation();

        // После нескольких таких сбоев readiness станет 503, чтобы показать критическую деградацию.
        readiness.RegisterCriticalFailure();
        logs.Add(data.ProcessKey, request.CorrelationId, "compensation", "Equipment step failed, participant notification was rolled back.");
        return new(data.ProcessKey, data.State, false, true, request.CorrelationId, "Compensation completed.");
    }

    private ProcessData GetOrCreate(string processKey)
    {
        if (_processes.TryGetValue(processKey, out var data)) return data;

        // Процесс создается лениво при первом событии, отдельный endpoint создания не нужен.
        data = new(processKey);
        _processes.Add(processKey, data);
        return data;
    }

    private static void Validate(BookingEventRequest request)
    {
        // Проверяем только обязательные поля. Неверный порядок событий ловит машина состояний ниже.
        if (string.IsNullOrWhiteSpace(request.ProcessKey))
            throw new AppException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "ProcessKey is required.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new AppException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "IdempotencyKey is required.");
        if (string.IsNullOrWhiteSpace(request.EventName))
            throw new AppException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "EventName is required.");
        if (string.IsNullOrWhiteSpace(request.CorrelationId))
            throw new AppException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "CorrelationId is required.");
    }

    private sealed class ProcessData(string processKey)
    {
        public string ProcessKey { get; } = processKey;
        public BookingState State { get; set; } = BookingState.New;
        public string? LastCorrelationId { get; set; }

        // Здесь хранятся уже обработанные события процесса: ключ - idempotencyKey, значение - первый ответ.
        public Dictionary<string, BookingEventResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
