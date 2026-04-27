namespace Task2.Core;

public sealed class ModuleException(string message) : InvalidOperationException(message);
