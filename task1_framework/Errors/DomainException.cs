namespace Pr1.MinWebService.Errors;

/// <summary>
/// Базовое исключение предметной области.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
