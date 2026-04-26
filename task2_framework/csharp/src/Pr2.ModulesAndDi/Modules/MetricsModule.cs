using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;

namespace Pr2.ModulesAndDi.Modules;

public sealed class MetricsModule : IAppModule
{
    public string Name => "Metrics";
    public IReadOnlyCollection<string> Requires => new[] { "Core", "Logging" };

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAppAction, MetricsAction>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private sealed class MetricsAction : IAppAction
    {
        public string Title => "Сбор системных метрик";
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var memory = GC.GetTotalMemory(false) / 1024;
            Console.WriteLine($"[МЕТРИКИ] Использовано памяти: {memory} KB.");
            return Task.CompletedTask;
        }
    }
}
