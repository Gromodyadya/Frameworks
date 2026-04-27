using System.Diagnostics;

namespace TaskCatalog.Api;

public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            logger.LogWarning("Request {RequestId} failed: {ErrorCode}", context.TraceIdentifier, ex.ErrorCode);
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(ex.ErrorCode, ex.Message, context.TraceIdentifier));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Request {RequestId} failed unexpectedly", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("INTERNAL_ERROR", "Unexpected server error.", context.TraceIdentifier));
        }
    }
}

public sealed class TimingMiddleware(ILogger<TimingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var watch = Stopwatch.StartNew();
        await next(context);
        watch.Stop();
        context.Items["ElapsedMs"] = watch.Elapsed.TotalMilliseconds;
        logger.LogInformation("Request {RequestId} completed in {ElapsedMs:F2} ms", context.TraceIdentifier, watch.Elapsed.TotalMilliseconds);
    }
}

public sealed class RequestLoggingMiddleware(RequestLogStore store, ILogger<RequestLoggingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        logger.LogInformation("Request {RequestId}: {Method} {Path}", context.TraceIdentifier, context.Request.Method, context.Request.Path);
        await next(context);
        var elapsed = context.Items.TryGetValue("ElapsedMs", out var value) ? Convert.ToDouble(value) : 0;
        store.Add(new(context.TraceIdentifier, context.Request.Method, context.Request.Path,
            context.Response.StatusCode, elapsed, DateTimeOffset.UtcNow));
        logger.LogInformation("Response {RequestId}: {StatusCode}", context.TraceIdentifier, context.Response.StatusCode);
    }
}
