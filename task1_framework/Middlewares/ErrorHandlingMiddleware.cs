using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pr1.MinWebService.Domain;
using Pr1.MinWebService.Errors;
using Pr1.MinWebService.Services;

namespace Pr1.MinWebService.Middlewares;

/// <summary>
/// Единая обработка ошибок и формирование согласованных ответов.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        // Гарантируем идентификатор запроса даже при сбое ранних шагов
        var requestId = RequestId.GetOrCreate(context);

        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Ошибка предметной области. requestId={RequestId}", requestId);
            await WriteError(context, ex.StatusCode, ex.Code, ex.Message, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непредвиденная ошибка. requestId={RequestId}", requestId);
            await WriteError(context, 500, "internal_error", "Внутренняя ошибка сервера", requestId);
        }
    }

    private static async Task WriteError(HttpContext context, int statusCode, string code, string message, string requestId)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new ErrorResponse(code, message, requestId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
