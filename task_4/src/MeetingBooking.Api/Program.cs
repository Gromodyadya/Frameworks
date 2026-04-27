using MeetingBooking.Api;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Enum выводится строкой, чтобы в отчете и при ручной проверке было видно "RoomReserved", а не число 1.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Все хранилища Singleton, потому что по заданию данные должны жить в памяти процесса.
builder.Services.AddSingleton<BookingStore>();
builder.Services.AddSingleton<ProcessLogStore>();
builder.Services.AddSingleton<MetricsStore>();
builder.Services.AddSingleton<ReadinessState>();

var app = builder.Build();

// Middleware стоит перед маршрутами, чтобы все ошибки API возвращались в одном JSON-формате.
app.UseMiddleware<ErrorMiddleware>();

app.MapGet("/", () => Results.Redirect("/health/live"));

// Главный endpoint: через него в машину состояний поступают все события процесса.
app.MapPost("/api/bookings/events", (BookingEventRequest request, BookingStore store) => store.Apply(request));
app.MapGet("/api/bookings/{processKey}", (string processKey, BookingStore store) => store.Get(processKey));
app.MapGet("/api/logs", (ProcessLogStore logs) => logs.All());
app.MapGet("/api/metrics", (MetricsStore metrics) => metrics.Snapshot());

// Liveness проверяет только то, что приложение запущено и отвечает.
app.MapGet("/health/live", () => Results.Ok(new { status = "Live" }));

// Readiness уже зависит от состояния сервиса: после критических сбоев он отвечает 503.
app.MapGet("/health/ready", (ReadinessState state) =>
    state.IsReady ? Results.Ok(new { status = "Ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

// Учебный endpoint для возврата readiness в норму после демонстрации деградации.
app.MapPost("/api/admin/recover", (ReadinessState state) => { state.Recover(); return Results.Ok(new { status = "Ready" }); });

app.Run();
