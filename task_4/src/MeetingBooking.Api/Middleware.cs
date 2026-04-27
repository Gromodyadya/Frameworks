using System.Text.Json;

namespace MeetingBooking.Api;

public sealed class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            // AppException - ожидаемая ошибка приложения, например неверный переход состояния.
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                errorCode = ex.Code,
                message = ex.Message,
                correlationId = context.TraceIdentifier
            }));
        }
        catch (Exception ex)
        {
            // Неожиданные ошибки не отдаем наружу со stack trace, чтобы не раскрывать внутренности сервиса.
            logger.LogError(ex, "Unhandled error");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                errorCode = "INTERNAL_ERROR",
                message = "Unexpected service error.",
                correlationId = context.TraceIdentifier
            }));
        }
    }
}
