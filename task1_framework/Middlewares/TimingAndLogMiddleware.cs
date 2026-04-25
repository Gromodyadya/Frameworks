using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pr1.MinWebService.Services;

namespace Pr1.MinWebService.Middlewares;

/// <summary>
/// Замер времени выполнения и запись краткого события в журнал.
/// </summary>
public sealed class TimingAndLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TimingAndLogMiddleware> _logger;

    public TimingAndLogMiddleware(RequestDelegate next, ILogger<TimingAndLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var requestId = RequestId.GetOrCreate(context);
        var sw = Stopwatch.StartNew();

        await _next(context);

        sw.Stop();

        _logger.LogInformation(
            "Запрос обработан. requestId={RequestId} method={Method} path={Path} status={Status} timeMs={TimeMs}",
            requestId,
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds
        );
    }
}
