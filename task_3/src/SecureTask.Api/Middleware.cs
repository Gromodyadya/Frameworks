using System.Collections.Concurrent;

namespace SecureTask.Api;

public sealed class ErrorMiddleware(RequestDelegate next, SecurityOptions options)
{
    public async Task Invoke(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            var app = ex as AppException;
            context.Response.StatusCode = app?.StatusCode ?? 500;
            var code = app?.ErrorCode ?? "INTERNAL_ERROR";
            // В боевом режиме не показываю лишние детали, в учебном оставляю их для проверки.
            var message = options.IsProduction && app is null ? "Request failed." : ex.Message;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(code, message, context.TraceIdentifier));
        }
    }
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        // Эти заголовки добавляются ко всем ответам как базовая защита браузера.
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        await next(context);
    }
}

public sealed class OriginGuardMiddleware(RequestDelegate next, SecurityOptions options)
{
    private readonly HashSet<string> _trusted = options.TrustedOrigins.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task Invoke(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        // Если Origin есть, это браузерный сценарий, поэтому сверяю его с белым списком.
        if (!string.IsNullOrWhiteSpace(origin) && !_trusted.Contains(origin))
            throw new AppException(403, "ORIGIN_FORBIDDEN",
                options.IsProduction ? "Forbidden." : $"Origin '{origin}' is not trusted.");
        if (!string.IsNullOrWhiteSpace(origin))
            context.Response.Headers.AccessControlAllowOrigin = origin;
        await next(context);
    }
}

public sealed class RateLimitMiddleware(RequestDelegate next, SecurityOptions options)
{
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    public async Task Invoke(HttpContext context)
    {
        // Создание данных ограничено строже, чем чтение, потому что это более дорогой маршрут.
        var limit = context.Request.Method == "POST" && context.Request.Path == "/api/items"
            ? options.RateLimit.CreateItemLimit : options.RateLimit.DefaultLimit;
        var key = $"{context.Connection.RemoteIpAddress}|{context.Request.Method}:{context.Request.Path}";
        var bucket = _buckets.GetOrAdd(key, _ => new(DateTimeOffset.UtcNow, 0));
        var allowed = bucket.Hit(options.RateLimit.WindowSeconds, limit);
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        if (!allowed) throw new AppException(429, "RATE_LIMIT_EXCEEDED",
            options.IsProduction ? "Too many requests." : $"Limit {limit} per route window exceeded.");
        await next(context);
    }

    private sealed class Bucket(DateTimeOffset windowStart, int count)
    {
        private readonly object _lock = new();
        private DateTimeOffset _windowStart = windowStart;
        private int _count = count;

        public bool Hit(int seconds, int limit)
        {
            lock (_lock)
            {
                if (DateTimeOffset.UtcNow - _windowStart > TimeSpan.FromSeconds(seconds))
                    // Когда окно времени закончилось, счетчик начинается заново.
                    (_windowStart, _count) = (DateTimeOffset.UtcNow, 0);
                return ++_count <= limit;
            }
        }
    }
}

public sealed class RequestLoggingMiddleware(RequestDelegate next, RequestLogStore logs)
{
    public async Task Invoke(HttpContext context)
    {
        await next(context);
        // Лог хранится в памяти: для задания этого достаточно, но после перезапуска он очищается.
        logs.Add(new(DateTimeOffset.UtcNow, context.Request.Method, context.Request.Path,
            context.Response.StatusCode, context.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
    }
}
