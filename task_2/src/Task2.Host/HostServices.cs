using Task2.Contracts;

namespace Task2.Host;

public sealed class RunLog : IRunLog
{
    private readonly List<string> _messages = [];
    public void Add(string message) => _messages.Add(message);
    public IReadOnlyList<string> Snapshot() => _messages.ToArray();
}

public sealed class RequestClock : IRequestClock
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
}

public sealed record ModuleRegistry(IReadOnlyList<string> Names)
{
    public static ModuleRegistry From(IEnumerable<IAppModule> modules) =>
        new(modules.Select(m => m.Name).ToArray());
}

public static class DemoOrders
{
    public static readonly Order[] All =
    [
        new(1, "Ada", 1200m),
        new(2, "Grace", 850m),
        new(3, "Linus", 430m)
    ];
}
