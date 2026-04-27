using Task2.Contracts;
using Task2.Core;
using Task2.Host;

var builder = WebApplication.CreateBuilder(args);
var options = ReadOptions(builder.Configuration, builder.Environment.ContentRootPath);

// Модули загружаются до Build(), потому что они должны успеть добавить сервисы в DI.
var modules = ModuleLoader.Load(options);

builder.Services.AddSingleton<IRunLog, RunLog>();
builder.Services.AddTransient<IRequestClock, RequestClock>();
builder.Services.AddSingleton(ModuleRegistry.From(modules));
ModuleRunner.Register(builder.Services, modules);

var app = builder.Build();
await ModuleRunner.InitializeAsync(app.Services, modules);

app.MapGet("/", (ModuleRegistry registry) => new { modules = registry.Names });
app.MapPost("/process", (IServiceProvider provider, IRunLog log) =>
{
    var orders = DemoOrders.All;

    // Все правила берутся из контейнера. Если модуль Validation отключить, правил здесь просто не будет.
    var errors = provider.GetServices<IDataRule>()
        .SelectMany(rule => orders.Select(rule.Check))
        .Where(result => !result.IsValid)
        .Select(result => result.Message)
        .ToArray();
    if (errors.Length > 0) return Results.BadRequest(new { errors });

    // Отчет и экспорт тоже необязательные: сервис появится только если подключен соответствующий модуль.
    var report = provider.GetService<IReportBuilder>()?.Build(orders);
    var exportPath = provider.GetService<IDataExporter>()?.Export(orders);
    return Results.Ok(new ProcessingResult(log.Snapshot(), report, exportPath));
});

app.Run();

static ModuleOptions ReadOptions(IConfiguration config, string root)
{
    // appsettings.json хранит имена модулей, папку с DLL и ожидаемую версию контракта.
    var modules = config.GetSection("Modules").GetChildren()
        .Select(section => section.Value ?? string.Empty)
        .Where(value => value.Length > 0)
        .ToArray();
    var directory = config["ModuleDirectory"] ?? "modules";
    var version = Version.Parse(config["ContractVersion"] ?? "1.0.0");
    return new(modules, Path.GetFullPath(Path.Combine(root, directory)), version);
}
