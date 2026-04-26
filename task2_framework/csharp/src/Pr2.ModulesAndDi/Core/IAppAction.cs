namespace Pr2.ModulesAndDi.Core;

/// <summary>
/// Действие, которое добавляет модуль.
/// </summary>
public interface IAppAction
{
    string Title { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
