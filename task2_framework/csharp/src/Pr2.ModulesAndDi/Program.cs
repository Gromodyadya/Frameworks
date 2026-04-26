using System.Reflection;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var enabled = configuration.GetSection("Modules").Get<string[]>() ?? Array.Empty<string>();

// --- ПОИСК МОДУЛЕЙ ВЕЗДЕ ---
var pluginsPath = Path.Combine(AppContext.BaseDirectory, "modules");
if (!Directory.Exists(pluginsPath))
{
    Directory.CreateDirectory(pluginsPath);
}

// Ищем модули внутри основной программы
var localModules = ModuleCatalog.DiscoverFromAssembly(Assembly.GetExecutingAssembly());
// Ищем модули во внешней папке "modules"
var externalModules = ModuleCatalog.DiscoverFromDirectory(pluginsPath);

// Объединяем все найденные модули в один список
var discovered = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase);
foreach (var m in localModules) discovered[m.Key] = m.Value;
foreach (var m in externalModules) discovered[m.Key] = m.Value;
// -------------------------------------------

var ordered = ModuleCatalog.BuildExecutionOrder(discovered, enabled);

var services = new ServiceCollection();

foreach (var module in ordered)
{
    module.RegisterServices(services);
}

var provider = services.BuildServiceProvider();

foreach (var module in ordered)
{
    await module.InitializeAsync(provider, CancellationToken.None);
}

var actions = provider.GetServices<IAppAction>().ToArray();

Console.WriteLine("Запуск действий модулей");
foreach (var action in actions)
{
    Console.WriteLine($"Действие {action.Title}");
    await action.ExecuteAsync(CancellationToken.None);
}
