using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;
using Pr2.ModulesAndDi.Services;

namespace Pr2.ModulesAndDi.Modules;

/// <summary>
/// Базовый модуль приложения.
/// </summary>
public sealed class CoreModule : IAppModule
{
    public string Name => "Core";

    public IReadOnlyCollection<string> Requires => Array.Empty<string>();

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IStorage, InMemoryStorage>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Базовый модуль не выполняет действий, он только подготавливает инфраструктуру
        return Task.CompletedTask;
    }
}
