namespace TaskCatalog.Api;

// Это исключение я использую для ожидаемых ошибок приложения:
// например, когда элемент не найден или входные данные не прошли проверку.
public sealed class AppException : Exception
{
    public AppException(int statusCode, string errorCode, string message) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    // HTTP-статус и код ошибки потом берет middleware и превращает в JSON-ответ.
    public int StatusCode { get; }
    public string ErrorCode { get; }
}
