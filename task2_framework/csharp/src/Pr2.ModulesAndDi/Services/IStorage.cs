namespace Pr2.ModulesAndDi.Services;

public interface IStorage
{
    void Add(string value);

    IReadOnlyList<string> GetAll();
}
