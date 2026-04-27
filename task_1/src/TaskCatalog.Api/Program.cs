using TaskCatalog.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddSingleton<TaskStore>();
builder.Services.AddSingleton<RequestLogStore>();
builder.Services.AddSingleton<TaskValidator>();
builder.Services.AddTransient<ExceptionHandlingMiddleware>();
builder.Services.AddTransient<TimingMiddleware>();
builder.Services.AddTransient<RequestLoggingMiddleware>();

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<TimingMiddleware>();

app.MapGet("/", () => Results.Redirect("/api/items"));

app.MapGet("/api/items", (
    TaskStore store,
    string? course,
    bool? completed,
    int? minDifficulty,
    int? maxDifficulty,
    string? sort) =>
{
    var query = new TaskQuery(course, completed, minDifficulty, maxDifficulty, sort);
    return Results.Ok(store.List(query));
});

app.MapGet("/api/items/{id:int}", (TaskStore store, int id) =>
    Results.Ok(store.Get(id)));

app.MapPost("/api/items", (TaskStore store, CreateStudyTaskRequest request) =>
{
    var item = store.Create(request);
    return Results.Created($"/api/items/{item.Id}", item);
});

app.MapGet("/api/logs", (RequestLogStore logs) => Results.Ok(logs.List()));

app.Run();
