namespace Task2.Contracts;

public sealed record Order(int Id, string Customer, decimal Amount);
public sealed record RuleResult(bool IsValid, string Message);
public sealed record ProcessingResult(IReadOnlyList<string> Log, string? Report, string? ExportPath);

public interface IRunLog
{
    void Add(string message);
    IReadOnlyList<string> Snapshot();
}

public interface IRequestClock
{
    Guid InstanceId { get; }
    DateTimeOffset Now { get; }
}

public interface IDataRule
{
    RuleResult Check(Order order);
}

public interface IReportBuilder
{
    string Build(IEnumerable<Order> orders);
}

public interface IDataExporter
{
    string Export(IEnumerable<Order> orders);
}
