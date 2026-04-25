using System.Collections.Concurrent;
using Pr1.MinWebService.Domain;

namespace Pr1.MinWebService.Services;

public sealed class InMemoryItemRepository : IItemRepository
{
    private readonly ConcurrentDictionary<Guid, Item> _items = new();

    public IReadOnlyCollection<Item> GetAll()
        => _items.Values
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase) // Сортируем по названию
            .ToArray();

    public Item? GetById(Guid id)
        => _items.TryGetValue(id, out var item) ? item : null;

    public Item Create(string title, int difficulty)
    {
        var id = Guid.NewGuid();
        var item = new Item(id, title, difficulty); // Создаем с новыми полями

        _items[id] = item;
        return item;
    }
}