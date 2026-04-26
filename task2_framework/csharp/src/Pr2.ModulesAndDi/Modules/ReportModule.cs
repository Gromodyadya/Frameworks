using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;
using Pr2.ModulesAndDi.Services;

namespace Pr2.ModulesAndDi.Modules;

public sealed class ReportModule : IAppModule
{
    public string Name => "Report";

    public IReadOnlyCollection<string> Requires => new[] { "Core", "Export" };

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAppAction, ReportAction>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private sealed class ReportAction : IAppAction
    {
        private readonly IClock _clock;
        private readonly IStorage _storage;

        public ReportAction(IClock clock, IStorage storage)
        {
            _clock = clock;
            _storage = storage;
        }

        public string Title => "Формирование отчёта";

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var count = _storage.GetAll().Count;
            Console.WriteLine($"Отчёт сформирован, время {_clock.Now}, записей {count}");
            return Task.CompletedTask;
        }
    }
}
