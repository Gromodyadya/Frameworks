using System.Collections.Concurrent;

namespace Pr2.ModulesAndDi.Services;

public sealed class InMemoryStorage : IStorage
{
    private readonly ConcurrentQueue<string> _values = new();

    public void Add(string value) => _values.Enqueue(value);

    public IReadOnlyList<string> GetAll() => _values.ToArray();
}
