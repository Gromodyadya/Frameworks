using Microsoft.Extensions.DependencyInjection;

namespace Task2.Contracts;

public static class ModuleContract
{
    // Версия нужна, чтобы случайно не подключить модуль от другого контракта.
    public static readonly Version Version = new(1, 0, 0);
}

public interface IAppModule
{
    string Name { get; }
    IReadOnlyCollection<string> RequiredModules { get; }
    Version ContractVersion { get; }

    // Первый шаг: модуль добавляет свои сервисы в общий DI-контейнер.
    void RegisterServices(IServiceCollection services);

    // Второй шаг: модуль выполняет стартовую логику уже после сборки контейнера.
    Task InitializeAsync(IServiceProvider provider, CancellationToken token);
}
