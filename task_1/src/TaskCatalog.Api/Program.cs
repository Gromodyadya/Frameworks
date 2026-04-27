using TaskCatalog.Api;

var builder = WebApplication.CreateBuilder(args);

// Оставляю только консольный лог, потому что для учебного проекта его проще смотреть
// прямо в терминале, а системный Windows Event Log может требовать лишние права.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Эти сервисы живут в приложении и потом автоматически передаются в endpoints и middleware.
// TaskStore сделан Singleton, чтобы задачи хранились в памяти между разными запросами.
builder.Services.AddSingleton<TaskStore>();
builder.Services.AddSingleton<RequestLogStore>();
builder.Services.AddSingleton<TaskValidator>();
builder.Services.AddTransient<ExceptionHandlingMiddleware>();
builder.Services.AddTransient<TimingMiddleware>();
builder.Services.AddTransient<RequestLoggingMiddleware>();

var app = builder.Build();

// Конвейер обработки запроса: сначала ловим ошибки, потом пишем журнал,
// потом измеряем время и передаем запрос в конкретный маршрут.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<TimingMiddleware>();

app.MapGet("/", () => Results.Redirect("/api/items"));

// Список задач поддерживает фильтры и сортировку через query-параметры.
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

// Если элемента нет, исключение из TaskStore перехватит ExceptionHandlingMiddleware.
app.MapGet("/api/items/{id:int}", (TaskStore store, int id) =>
    Results.Ok(store.Get(id)));

// При создании возвращаю 201 Created и ссылку на новый ресурс.
app.MapPost("/api/items", (TaskStore store, CreateStudyTaskRequest request) =>
{
    var item = store.Create(request);
    return Results.Created($"/api/items/{item.Id}", item);
});

// Этот маршрут добавлен, чтобы можно было увидеть requestId и время обработки запросов.
app.MapGet("/api/logs", (RequestLogStore logs) => Results.Ok(logs.List()));

app.Run();
