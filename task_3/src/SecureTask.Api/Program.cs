using SecureTask.Api;

var options = SecurityOptions.Load(args);
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// Режим влияет только через настройки: в Production журнал делается тише.
if (options.IsProduction) builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<TaskStore>();
builder.Services.AddSingleton<RequestLogStore>();

var app = builder.Build();
// Порядок middleware важен: сначала ошибки, потом защитные проверки и только потом маршруты.
app.UseMiddleware<ErrorMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<OriginGuardMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapGet("/api/config", (SecurityOptions cfg) => new
{
    cfg.Mode,
    cfg.TrustedOrigins,
    cfg.RateLimit.WindowSeconds,
    cfg.RateLimit.DefaultLimit,
    cfg.RateLimit.CreateItemLimit
});
app.MapGet("/api/items", (TaskStore store) => store.All());
app.MapPost("/api/items", (CreateTaskRequest request, TaskStore store) => Results.Created("/api/items", store.Add(request)));
app.MapGet("/api/logs", (RequestLogStore logs) => logs.All());
app.Run();
