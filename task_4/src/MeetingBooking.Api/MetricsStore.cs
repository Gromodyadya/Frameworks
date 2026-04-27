using System.Collections.Concurrent;

namespace MeetingBooking.Api;

public sealed class MetricsStore
{
    // Счетчики long + Interlocked нужны, чтобы инкременты были безопасны при параллельных запросах.
    private long _successfulTransitions;
    private long _failedTransitions;
    private long _duplicateDeliveries;
    private long _compensations;

    // Для задержки храним не все значения, а только количество и сумму миллисекунд по каждому шагу.
    private readonly ConcurrentDictionary<string, (long Count, long TotalMs)> _latency = new();

    public void Success(string step, long elapsedMs)
    {
        Interlocked.Increment(ref _successfulTransitions);

        // AddOrUpdate одновременно добавляет первый замер или обновляет накопленную сумму.
        _latency.AddOrUpdate(step, (1, elapsedMs), (_, old) => (old.Count + 1, old.TotalMs + elapsedMs));
    }

    public void Failure() => Interlocked.Increment(ref _failedTransitions);
    public void Duplicate() => Interlocked.Increment(ref _duplicateDeliveries);
    public void Compensation() => Interlocked.Increment(ref _compensations);

    public MetricsSnapshot Snapshot()
    {
        // Среднее считаем в момент запроса, чтобы не хранить отдельный список всех задержек.
        var latency = _latency.ToDictionary(x => x.Key, x => Math.Round((double)x.Value.TotalMs / x.Value.Count, 2));
        return new(Interlocked.Read(ref _successfulTransitions), Interlocked.Read(ref _failedTransitions),
            Interlocked.Read(ref _duplicateDeliveries), Interlocked.Read(ref _compensations), latency);
    }
}
