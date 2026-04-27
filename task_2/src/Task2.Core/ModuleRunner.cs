using Microsoft.Extensions.DependencyInjection;
using Task2.Contracts;

namespace Task2.Core;

public static class ModuleRunner
{
    public static void Register(IServiceCollection services, IEnumerable<IAppModule> modules)
    {
        // Здесь ядро не знает конкретные классы сервисов, оно просто дает модулям зарегистрироваться.
        foreach (var module in modules) module.RegisterServices(services);
    }

    public static async Task InitializeAsync(IServiceProvider provider,
        IEnumerable<IAppModule> modules, CancellationToken token = default)
    {
        // Инициализация идет уже в отсортированном порядке: сначала зависимости, потом зависимые модули.
        foreach (var module in modules)
            await module.InitializeAsync(provider, token);
    }
}
