namespace Pr2.ModulesAndDi.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
}
