using Microsoft.Extensions.DependencyInjection;
using Task2.Contracts;

namespace Task2.ValidationModule;

public sealed class ValidationModule : IAppModule
{
    public string Name => "Validation";
    public IReadOnlyCollection<string> RequiredModules => [];
    public Version ContractVersion => ModuleContract.Version;

    // Правило проверки добавляется в контейнер как обычный сервис.
    public void RegisterServices(IServiceCollection services) =>
        services.AddSingleton<IDataRule, PositiveAmountRule>();

    public Task InitializeAsync(IServiceProvider provider, CancellationToken token)
    {
        provider.GetRequiredService<IRunLog>().Add("Validation initialized");
        return Task.CompletedTask;
    }
}

public sealed class PositiveAmountRule : IDataRule
{
    // Для примера считаем заказ корректным, если сумма больше нуля.
    public RuleResult Check(Order order) => order.Amount > 0
        ? new(true, $"Order {order.Id} is valid")
        : new(false, $"Order {order.Id} must have positive amount");
}
