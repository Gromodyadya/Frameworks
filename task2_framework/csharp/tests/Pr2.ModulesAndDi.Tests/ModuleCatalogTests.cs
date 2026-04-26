using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;
using Xunit;

namespace Pr2.ModulesAndDi.Tests;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void Порядок_запуска_учитывает_зависимости()
    {
        var a = new FakeModule("A", Array.Empty<string>());
        var b = new FakeModule("B", new[] { "A" });
        var c = new FakeModule("C", new[] { "B" });

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a,
            [b.Name] = b,
            [c.Name] = c
        };

        var order = ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B", "C" });

        Assert.Equal(new[] { "A", "B", "C" }, order.Select(m => m.Name).ToArray());
    }

    [Fact]
    public void Отсутствующий_модуль_даёт_понятную_ошибку()
    {
        var a = new FakeModule("A", Array.Empty<string>());

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a
        };

        var ex = Assert.Throws<ModuleLoadException>(() => ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B" }));
        Assert.Contains("Модуль не найден", ex.Message);
    }

    [Fact]
    public void Цикл_зависимостей_обнаруживается()
    {
        var a = new FakeModule("A", new[] { "B" });
        var b = new FakeModule("B", new[] { "A" });

        var all = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase)
        {
            [a.Name] = a,
            [b.Name] = b
        };

        var ex = Assert.Throws<ModuleLoadException>(() => ModuleCatalog.BuildExecutionOrder(all, new[] { "A", "B" }));
        Assert.Contains("циклическая", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Внедрение_зависимостей_работает()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MarkerService>();

        var provider = services.BuildServiceProvider();

        var module = new FakeModule("A", Array.Empty<string>())
        {
            OnInit = sp =>
            {
                var s = sp.GetService<MarkerService>();
                Assert.NotNull(s);
            }
        };

        await module.InitializeAsync(provider, CancellationToken.None);
    }

    private sealed class MarkerService { }

    private sealed class FakeModule : IAppModule
    {
        public FakeModule(string name, IReadOnlyCollection<string> requires)
        {
            Name = name;
            Requires = requires;
        }

        public string Name { get; }

        public IReadOnlyCollection<string> Requires { get; }

        public Action<IServiceProvider>? OnInit { get; init; }

        public void RegisterServices(IServiceCollection services) { }

        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            OnInit?.Invoke(serviceProvider);
            return Task.CompletedTask;
        }
    }
}
