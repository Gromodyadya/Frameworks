namespace Task2.Core;

public sealed record ModuleOptions(
    IReadOnlyList<string> Modules,
    string ModuleDirectory,
    Version ContractVersion);
