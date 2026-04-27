using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskCatalog.Api;

// Это простой тестовый runner без xUnit/NUnit, чтобы не добавлять внешние пакеты.
// Каждый пункт запускает отдельный важный сценарий из задания.
var tests = new List<(string Name, Func<Task> Run)>
{
    ("validation rejects invalid values", TestValidation),
    ("api creates and reads item", TestCreateAndRead),
    ("api returns unified not found error", TestNotFound),
    ("api filters, sorts and writes logs", TestFilterSortAndLogs)
};

// Тесты поднимают настоящий API-процесс и проверяют его через HTTP,
// поэтому это ближе к реальной проверке работы веб-службы.
var app = await ApiProcess.StartAsync();
try
{
    foreach (var test in tests)
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
}
finally
{
    app.Dispose();
}

static Task TestValidation()
{
    // Здесь проверяю чистую логику предметной области без запуска HTTP.
    var validator = new TaskValidator();
    ExpectThrows(() => validator.Validate(new("", "Frameworks", 3)), "empty title");
    ExpectThrows(() => validator.Validate(new("Task", "Frameworks", 0)), "difficulty");
    ExpectThrows(() => validator.Validate(new("Task", "Frameworks", 3, Notes: new string('x', 251))), "notes limit");
    return Task.CompletedTask;
}

static async Task TestCreateAndRead()
{
    // Главный успешный сценарий: создали задачу, затем получили ее обратно по id.
    var created = await ApiProcess.Client.PostAsJsonAsync("/api/items", new CreateStudyTaskRequest("Test API", "Frameworks", 4));
    Assert(created.StatusCode == HttpStatusCode.Created, "create status");
    var item = await created.Content.ReadFromJsonAsync<StudyTask>() ?? throw new Exception("empty item");
    var loaded = await ApiProcess.Client.GetFromJsonAsync<StudyTask>($"/api/items/{item.Id}");
    Assert(loaded?.Title == "Test API" && loaded.Difficulty == 4, "loaded item");
}

static async Task TestNotFound()
{
    // Проверяю, что ошибка 404 приходит в едином формате, а не случайным текстом.
    var response = await ApiProcess.Client.GetAsync("/api/items/99999");
    Assert(response.StatusCode == HttpStatusCode.NotFound, "not found status");
    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>() ?? throw new Exception("empty error");
    Assert(error.ErrorCode == "ITEM_NOT_FOUND" && !string.IsNullOrWhiteSpace(error.RequestId), "error shape");
}

static async Task TestFilterSortAndLogs()
{
    // Этот сценарий проверяет усложнения: фильтрацию, сортировку и журналирование.
    await ApiProcess.Client.PostAsJsonAsync("/api/items", new CreateStudyTaskRequest("Z task", "Math", 5));
    await ApiProcess.Client.PostAsJsonAsync("/api/items", new CreateStudyTaskRequest("A task", "Math", 1, true));
    var items = await ApiProcess.Client.GetFromJsonAsync<List<StudyTask>>("/api/items?course=Math&sort=title");
    Assert(items is { Count: >= 2 } && items[0].Title == "A task", "filter sort");
    await Task.Delay(100);
    var logs = await ApiProcess.Client.GetFromJsonAsync<List<RequestLogEntry>>("/api/logs");
    Assert(logs is { Count: > 0 } && logs.Any(x => !string.IsNullOrWhiteSpace(x.RequestId) && x.ElapsedMs > 0), "logs");
}

static void ExpectThrows(Action action, string name)
{
    try { action(); }
    catch (AppException) { return; }
    throw new Exception($"Expected validation failure: {name}");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception($"Assertion failed: {message}");
}

sealed class ApiProcess : IDisposable
{
    public static readonly HttpClient Client = new() { BaseAddress = new Uri("http://127.0.0.1:5107") };
    private readonly Process _process;

    private ApiProcess(Process process) => _process = process;

    public static async Task<ApiProcess> StartAsync()
    {
        var root = FindRoot();
        var apiProject = Path.Combine(root, "src", "TaskCatalog.Api", "TaskCatalog.Api.csproj");

        // Запускаю API как отдельный процесс, чтобы тестировать его как обычный клиент.
        var info = new ProcessStartInfo("dotnet", $"run --project \"{apiProject}\" --urls http://127.0.0.1:5107")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.Environment["DOTNET_CLI_HOME"] = Path.Combine(root, ".dotnet");
        var process = Process.Start(info) ?? throw new Exception("API process was not started.");

        // Потоки вывода нужно читать, иначе у процесса может заполниться буфер вывода.
        _ = Task.Run(() => process.StandardOutput.ReadToEndAsync());
        _ = Task.Run(() => process.StandardError.ReadToEndAsync());
        await WaitUntilReady(process);
        return new ApiProcess(process);
    }

    private static async Task WaitUntilReady(Process process)
    {
        // Сервер стартует не мгновенно, поэтому жду, пока он начнет отвечать на запросы.
        for (var i = 0; i < 60; i++)
        {
            if (process.HasExited) throw new Exception("API process exited before readiness.");
            try { _ = await Client.GetAsync("/api/items"); return; }
            catch { await Task.Delay(250); }
        }
        throw new TimeoutException("API readiness timeout.");
    }

    private static string FindRoot()
    {
        // При запуске из bin/Debug нужно подняться вверх до корня решения.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TaskFramework.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Solution root was not found.");
    }

    public void Dispose()
    {
        // После тестов обязательно останавливаю API, чтобы порт 5107 не оставался занятым.
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        _process.Dispose();
    }
}
