using Pr1.MinWebService.Domain;

namespace Pr1.MinWebService.Services;

public interface IItemRepository
{
    IReadOnlyCollection<Item> GetAll();
    Item? GetById(Guid id);
    Item Create(string title, int difficulty); // Изменил параметры
}