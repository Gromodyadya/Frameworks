using System.Reflection;
using System.IO;

namespace Pr2.ModulesAndDi.Core;

public static class ModuleCatalog
{
    public static IReadOnlyDictionary<string, IAppModule> DiscoverFromAssembly(Assembly assembly)
    {
        var modules = assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => typeof(IAppModule).IsAssignableFrom(t))
            .Select(t => (IAppModule)Activator.CreateInstance(t)!)
            .ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        return modules;
    }

    // --- НАШ НОВЫЙ МЕТОД ДЛЯ ЗАГРУЗКИ ВНЕШНИХ DLL ---
    public static IReadOnlyDictionary<string, IAppModule> DiscoverFromDirectory(string directoryPath)
    {
        var modules = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directoryPath))
            return modules;

        var dllFiles = Directory.GetFiles(directoryPath, "*.dll");

        foreach (var file in dllFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(file);
                var discoveredInAssembly = DiscoverFromAssembly(assembly);

                foreach (var kvp in discoveredInAssembly)
                {
                    modules[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Внимание] Не удалось загрузить сборку {file}: {ex.Message}");
            }
        }

        return modules;
    }
    // ------------------------------------------------

    public static IReadOnlyList<IAppModule> BuildExecutionOrder(
        IReadOnlyDictionary<string, IAppModule> all,
        IReadOnlyCollection<string> enabledNames)
    {
        var enabled = new Dictionary<string, IAppModule>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in enabledNames)
        {
            if (!all.TryGetValue(name, out var module))
                throw new ModuleLoadException($"Модуль не найден, имя модуля {name}");

            enabled[name] = module;
        }

        foreach (var module in enabled.Values)
        {
            foreach (var req in module.Requires)
            {
                if (!enabled.ContainsKey(req))
                    throw new ModuleLoadException($"Не хватает модуля для зависимости, модуль {module.Name} требует {req}");
            }
        }

        // Kahn algorithm
        var indegree = enabled.Values.ToDictionary(m => m.Name, _ => 0, StringComparer.OrdinalIgnoreCase);
        var edges = enabled.Values.ToDictionary(m => m.Name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var module in enabled.Values)
        {
            foreach (var req in module.Requires)
            {
                edges[req].Add(module.Name);
                indegree[module.Name] += 1;
            }
        }

        var queue = new Queue<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<IAppModule>();

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            result.Add(enabled[name]);

            foreach (var to in edges[name])
            {
                indegree[to] -= 1;
                if (indegree[to] == 0)
                    queue.Enqueue(to);
            }
        }

        if (result.Count != enabled.Count)
        {
            var stuck = indegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToArray();
            var list = string.Join(", ", stuck);
            throw new ModuleLoadException($"Обнаружена циклическая зависимость модулей, проблемные модули {list}");
        }

        return result;
    }
}
