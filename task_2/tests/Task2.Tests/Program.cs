using Microsoft.Extensions.DependencyInjection;
using Task2.Contracts;
using Task2.Core;

var tests = new (string Name, Action Test)[]
{
    // Это простые проверки без xUnit, чтобы проект запускался даже без дополнительных пакетов.
    ("sorts linear dependencies", SortsLinear),
    ("sorts independent dependency sets", SortsIndependent),
    ("reports missing module", ReportsMissing),
    ("reports cyclic dependency", ReportsCycle),
    ("injects dependencies through container", InjectsDependencies)
};

foreach (var (name, test) in tests)
{
    test();
    Console.WriteLine($"PASS: {name}");
}

static void SortsLinear()
{
    var order = ModuleSorter.Sort([
        new FakeModule("Export", ["Report"]),
        new FakeModule("Report", ["Validation"]),
        new FakeModule("Validation")
    ]);
    AssertEqual("Validation,Report,Export", string.Join(",", order.Select(m => m.Name)));
}

static void SortsIndependent()
{
    var order = ModuleSorter.Sort([
        new FakeModule("B", ["A"]),
        new FakeModule("D", ["C"]),
        new FakeModule("A"),
        new FakeModule("C")
    ]);
    AssertBefore(order, "A", "B");
    AssertBefore(order, "C", "D");
}

static void ReportsMissing() =>
    AssertThrows("Missing module 'Unknown'", () => ModuleSorter.Sort([new FakeModule("A", ["Unknown"])]));

static void ReportsCycle() =>
    AssertThrows("Cyclic dependency detected", () => ModuleSorter.Sort([
        new FakeModule("A", ["B"]),
        new FakeModule("B", ["A"])
    ]));

static void InjectsDependencies()
{
    var services = new ServiceCollection();
    services.AddSingleton<IRunLog, TestLog>();
    ModuleRunner.Register(services, [new DiProbeModule()]);
    using var provider = services.BuildServiceProvider();

    // NeedsLog создается контейнером. Если DI не работает, зависимость IRunLog сюда не попадет.
    provider.GetRequiredService<NeedsLog>().Touch();
    AssertEqual("created by DI", provider.GetRequiredService<IRunLog>().Snapshot().Single());
}

static void AssertBefore(IReadOnlyList<IAppModule> order, string first, string second)
{
    if (order.ToList().FindIndex(m => m.Name == first) > order.ToList().FindIndex(m => m.Name == second))
        throw new Exception($"{first} must be before {second}");
}

static void AssertThrows(string text, Action action)
{
    try { action(); } catch (ModuleException ex) when (ex.Message.Contains(text)) { return; }
    throw new Exception($"Expected error containing '{text}'");
}

static void AssertEqual(string expected, string actual)
{
    if (expected != actual) throw new Exception($"Expected '{expected}', got '{actual}'");
}

class FakeModule(string name, IReadOnlyCollection<string>? required = null) : IAppModule
{
    public string Name => name;
    public IReadOnlyCollection<string> RequiredModules => required ?? [];
    public Version ContractVersion => ModuleContract.Version;
    public virtual void RegisterServices(IServiceCollection services) { }
    public Task InitializeAsync(IServiceProvider provider, CancellationToken token) => Task.CompletedTask;
}

sealed class DiProbeModule : FakeModule
{
    public DiProbeModule() : base("Probe") { }
    public override void RegisterServices(IServiceCollection services) => services.AddTransient<NeedsLog>();
}

sealed class NeedsLog(IRunLog log)
{
    public void Touch() => log.Add("created by DI");
}

sealed class TestLog : IRunLog
{
    private readonly List<string> _messages = [];
    public void Add(string message) => _messages.Add(message);
    public IReadOnlyList<string> Snapshot() => _messages;
}
