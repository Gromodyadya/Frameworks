using Task2.Contracts;

namespace Task2.Core;

public static class ModuleSorter
{
    public static IReadOnlyList<IAppModule> Sort(IEnumerable<IAppModule> modules)
    {
        var map = modules.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<IAppModule>();
        var path = new Stack<string>();

        // Каждый модуль обходим рекурсивно, чтобы сначала добавить его зависимости.
        foreach (var name in map.Keys)
            Visit(name, map, states, result, path);

        return result;
    }

    private static void Visit(string name, IReadOnlyDictionary<string, IAppModule> map,
        IDictionary<string, int> states, ICollection<IAppModule> result, Stack<string> path)
    {
        if (!map.TryGetValue(name, out var module))
            throw new ModuleException($"Missing module '{name}'. Add it to settings and modules directory.");
        if (states.TryGetValue(name, out var state) && state == 2) return;

        // state == 1 значит, что мы снова пришли в модуль, который уже есть в текущей цепочке.
        if (state == 1) throw new ModuleException($"Cyclic dependency detected: {Cycle(path, name)}.");

        states[name] = 1;
        path.Push(name);
        foreach (var dependency in module.RequiredModules) Visit(dependency, map, states, result, path);
        path.Pop();
        states[name] = 2;

        // Добавляем модуль только после зависимостей, поэтому порядок запуска получается правильным.
        result.Add(module);
    }

    private static string Cycle(IEnumerable<string> path, string name) =>
        string.Join(" -> ", path.Reverse().Concat([name]));
}
