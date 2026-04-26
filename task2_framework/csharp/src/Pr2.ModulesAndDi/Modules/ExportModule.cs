using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;
using Pr2.ModulesAndDi.Services;

namespace Pr2.ModulesAndDi.Modules;

public sealed class ExportModule : IAppModule
{
    public string Name => "Export";

    public IReadOnlyCollection<string> Requires => new[] { "Core", "Validation" };

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAppAction, ExportAction>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private sealed class ExportAction : IAppAction
    {
        private readonly IStorage _storage;

        public ExportAction(IStorage storage) => _storage = storage;

        public string Title => "Экспорт данных в файл";

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var lines = _storage.GetAll();
            var path = Path.Combine(AppContext.BaseDirectory, "export.txt");
            await File.WriteAllLinesAsync(path, lines, cancellationToken);
        }
    }
}
