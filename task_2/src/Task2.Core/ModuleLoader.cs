using System.Reflection;
using Task2.Contracts;

namespace Task2.Core;

public static class ModuleLoader
{
    public static IReadOnlyList<IAppModule> Load(ModuleOptions options)
    {
        if (!Directory.Exists(options.ModuleDirectory))
            throw new ModuleException($"Module directory '{options.ModuleDirectory}' was not found.");

        // Берем только те модули, которые явно указаны в настройках приложения.
        var requested = new HashSet<string>(options.Modules, StringComparer.OrdinalIgnoreCase);
        var modules = Directory.EnumerateFiles(options.ModuleDirectory, "*.dll")
            .SelectMany(LoadModules)
            .Where(m => requested.Contains(m.Name))
            .ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        // Если имя есть в appsettings.json, но DLL или класса модуля нет, сразу пишем понятную ошибку.
        var missing = requested.Except(modules.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        if (missing.Length > 0)
            throw new ModuleException($"Missing configured module(s): {string.Join(", ", missing)}.");

        // Проверяется major-версия: модуль с другим контрактом лучше не запускать.
        foreach (var module in modules.Values)
            if (module.ContractVersion.Major != options.ContractVersion.Major)
                throw new ModuleException($"Module '{module.Name}' uses incompatible contract {module.ContractVersion}.");

        return ModuleSorter.Sort(modules.Values);
    }

    private static IEnumerable<IAppModule> LoadModules(string file)
    {
        // Reflection позволяет найти модуль в DLL без прямой ссылки на его проект.
        var assembly = Assembly.LoadFrom(Path.GetFullPath(file));
        return assembly.GetTypes()
            .Where(t => typeof(IAppModule).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (IAppModule)Activator.CreateInstance(t)!);
    }
}
