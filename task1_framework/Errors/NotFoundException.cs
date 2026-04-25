namespace Pr1.MinWebService.Errors;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message)
        : base(code: "not_found", message: message, statusCode: 404)
    {
    }
}
