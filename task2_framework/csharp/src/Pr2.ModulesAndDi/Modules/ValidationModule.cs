using Microsoft.Extensions.DependencyInjection;
using Pr2.ModulesAndDi.Core;
using Pr2.ModulesAndDi.Services;

namespace Pr2.ModulesAndDi.Modules;

public sealed class ValidationModule : IAppModule
{
    public string Name => "Validation";

    public IReadOnlyCollection<string> Requires => new[] { "Core" };

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAppAction, ValidationAction>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private sealed class ValidationAction : IAppAction
    {
        private readonly IStorage _storage;

        public ValidationAction(IStorage storage) => _storage = storage;

        public string Title => "Проверка правил данных";

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var value = "пример";
            if (value.Length < 3)
                throw new Exception("Значение слишком короткое");

            _storage.Add(value);
            return Task.CompletedTask;
        }
    }
}
