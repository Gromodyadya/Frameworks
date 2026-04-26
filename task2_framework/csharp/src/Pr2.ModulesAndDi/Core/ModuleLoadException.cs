namespace Pr2.ModulesAndDi.Core;

/// <summary>
/// Ошибка загрузки или запуска модулей.
/// </summary>
public sealed class ModuleLoadException : Exception
{
    public ModuleLoadException(string message)
        : base(message)
    {
    }
}
