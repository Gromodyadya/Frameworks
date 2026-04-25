namespace Pr1.MinWebService.Domain;

/// <summary>
/// Единый формат ошибки для клиентов.
/// </summary>
public sealed record ErrorResponse(string Code, string Message, string RequestId);
