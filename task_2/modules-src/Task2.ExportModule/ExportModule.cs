using Microsoft.Extensions.DependencyInjection;
using Task2.Contracts;

namespace Task2.ExportModule;

public sealed class ExportModule : IAppModule
{
    public string Name => "Export";
    public IReadOnlyCollection<string> RequiredModules => ["Reporting"];
    public Version ContractVersion => ModuleContract.Version;

    // Экспортер singleton, потому что у него нет состояния на один конкретный запрос.
    public void RegisterServices(IServiceCollection services) =>
        services.AddSingleton<IDataExporter, CsvExporter>();

    public Task InitializeAsync(IServiceProvider provider, CancellationToken token)
    {
        // HashCode выводится в журнал, чтобы было видно один и тот же singleton-экземпляр.
        var exporter = provider.GetRequiredService<IDataExporter>();
        provider.GetRequiredService<IRunLog>().Add($"Export initialized as singleton {exporter.GetHashCode()}");
        return Task.CompletedTask;
    }
}

public sealed class CsvExporter : IDataExporter
{
    public string Export(IEnumerable<Order> orders)
    {
        // Файл создается в папке artifacts внутри запущенного Host-проекта.
        Directory.CreateDirectory("artifacts");
        var path = Path.GetFullPath(Path.Combine("artifacts", "orders.csv"));
        var lines = orders.Select(o => $"{o.Id};{o.Customer};{o.Amount:0.00}");
        File.WriteAllLines(path, ["id;customer;amount", .. lines]);
        return path;
    }
}
