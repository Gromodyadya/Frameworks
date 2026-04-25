namespace Pr1.MinWebService.Errors;

public sealed class ValidationException : DomainException
{
    public ValidationException(string message)
        : base(code: "validation", message: message, statusCode: 400)
    {
    }
}
