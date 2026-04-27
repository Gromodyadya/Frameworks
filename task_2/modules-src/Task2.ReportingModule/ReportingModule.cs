using Microsoft.Extensions.DependencyInjection;
using Task2.Contracts;

namespace Task2.ReportingModule;

public sealed class ReportingModule : IAppModule
{
    public string Name => "Reporting";
    public IReadOnlyCollection<string> RequiredModules => ["Validation"];
    public Version ContractVersion => ModuleContract.Version;

    // Отчет transient: при каждом запросе контейнер может создать новый ReportBuilder.
    public void RegisterServices(IServiceCollection services) =>
        services.AddTransient<IReportBuilder, ReportBuilder>();

    public Task InitializeAsync(IServiceProvider provider, CancellationToken token)
    {
        // Два разных Guid показывают, что IRequestClock действительно transient.
        var first = provider.GetRequiredService<IRequestClock>().InstanceId;
        var second = provider.GetRequiredService<IRequestClock>().InstanceId;
        provider.GetRequiredService<IRunLog>().Add($"Reporting initialized, transient clocks: {first} != {second}");
        return Task.CompletedTask;
    }
}

public sealed class ReportBuilder(IEnumerable<IDataRule> rules) : IReportBuilder
{
    public string Build(IEnumerable<Order> orders)
    {
        // ReportBuilder сам не создает правила, а получает их через DI.
        var valid = orders.Count(order => rules.All(rule => rule.Check(order).IsValid));
        return $"Report: {valid} valid order(s), total amount {orders.Sum(o => o.Amount):0.00}";
    }
}
