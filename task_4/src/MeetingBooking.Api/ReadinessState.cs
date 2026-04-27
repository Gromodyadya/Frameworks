namespace MeetingBooking.Api;

public sealed class ReadinessState
{
    private int _criticalFailures;

    // После двух критических сбоев сервис считается живым, но не готовым принимать новый трафик.
    public bool IsReady => Volatile.Read(ref _criticalFailures) < 2;

    // Interlocked нужен, потому что сбои могут прийти из разных HTTP-запросов одновременно.
    public void RegisterCriticalFailure() => Interlocked.Increment(ref _criticalFailures);

    // Метод восстановления оставлен для демонстрации: им можно вернуть /health/ready в 200 OK.
    public void Recover() => Interlocked.Exchange(ref _criticalFailures, 0);
}
