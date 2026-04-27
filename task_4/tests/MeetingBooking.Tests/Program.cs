using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

var root = FindRoot();
var api = Path.Combine(root, "src", "MeetingBooking.Api", "MeetingBooking.Api.csproj");

// Это не xUnit, а простая консольная проверка: так меньше зависимостей и легче запускать в учебной среде.
var tests = new (string Name, Func<Task> Run)[]
{
    ("state machine completes booking", CompletesBooking),
    ("duplicate delivery is idempotent", DuplicateDelivery),
    ("failed step runs compensation", Compensation),
    ("readiness fails after critical degradation", ReadinessDegrades),
    ("logs and metrics are observable", LogsAndMetrics)
};

foreach (var test in tests)
{
    await test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

async Task CompletesBooking()
{
    using var server = await ApiProcess.Start(api, 5401);

    // Проверяем нормальный путь машины состояний из четырех событий.
    await Send(server.Client, "complete-1", "k1", "ReserveRoom", "c1");
    await Send(server.Client, "complete-1", "k2", "NotifyParticipants", "c2");
    await Send(server.Client, "complete-1", "k3", "PrepareEquipment", "c3");
    var result = await Send(server.Client, "complete-1", "k4", "ConfirmBooking", "c4");

    Assert(result.GetProperty("state").GetString() == "Completed", "booking must finish in Completed state");
}

async Task DuplicateDelivery()
{
    using var server = await ApiProcess.Start(api, 5402);

    // Один и тот же idempotencyKey отправляется два раза, как будто брокер повторно доставил событие.
    await Send(server.Client, "dup-1", "same-key", "ReserveRoom", "corr-a");
    var duplicate = await Send(server.Client, "dup-1", "same-key", "ReserveRoom", "corr-b");
    var process = await server.Client.GetFromJsonAsync<JsonElement>("/api/bookings/dup-1");

    Assert(duplicate.GetProperty("duplicate").GetBoolean(), "second event must be marked as duplicate");
    Assert(process.GetProperty("state").GetString() == "RoomReserved", "duplicate must not move state twice");
}

async Task Compensation()
{
    using var server = await ApiProcess.Start(api, 5403);

    // Доводим процесс до состояния ParticipantsNotified, потом имитируем сбой подготовки оборудования.
    await Send(server.Client, "fail-1", "k1", "ReserveRoom", "c1");
    await Send(server.Client, "fail-1", "k2", "NotifyParticipants", "c2");
    var result = await Send(server.Client, "fail-1", "k3", "PrepareEquipment", "c3", fail: true);

    Assert(result.GetProperty("compensated").GetBoolean(), "failed equipment step must run compensation");
    Assert(result.GetProperty("state").GetString() == "RoomReserved", "compensation must return to reserved room state");
}

async Task ReadinessDegrades()
{
    using var server = await ApiProcess.Start(api, 5404);

    // Два критических сбоя подряд должны перевести readiness в 503.
    for (var i = 0; i < 2; i++)
    {
        await Send(server.Client, $"bad-{i}", "k1", "ReserveRoom", $"c{i}");
        await Send(server.Client, $"bad-{i}", "k2", "NotifyParticipants", $"c{i}");
        await Send(server.Client, $"bad-{i}", "k3", "PrepareEquipment", $"c{i}", fail: true);
    }

    var ready = await server.Client.GetAsync("/health/ready");
    Assert(ready.StatusCode == HttpStatusCode.ServiceUnavailable, "readiness must fail after critical degradation");
}

async Task LogsAndMetrics()
{
    using var server = await ApiProcess.Start(api, 5405);

    // Сначала проверяем, что correlationId попадает в журнал и что повтор считается метрикой.
    await Send(server.Client, "obs-1", "k1", "ReserveRoom", "trace-1");
    await Send(server.Client, "obs-1", "k1", "ReserveRoom", "trace-2");
    var logs = await server.Client.GetFromJsonAsync<JsonElement>("/api/logs");
    var metrics = await server.Client.GetFromJsonAsync<JsonElement>("/api/metrics");
    var logText = logs.ToString();
    Assert(logText.Contains("trace-1") && logText.Contains("trace-2"), "logs must contain correlation ids");
    Assert(metrics.GetProperty("successfulTransitions").GetInt64() == 1, "metrics must count success");
    Assert(metrics.GetProperty("duplicateDeliveries").GetInt64() == 1, "metrics must count duplicates");

    // Потом отдельно проверяем счетчики ошибки и компенсации.
    await Send(server.Client, "obs-2", "k1", "ReserveRoom", "trace-3");
    await Send(server.Client, "obs-2", "k2", "NotifyParticipants", "trace-4");
    await Send(server.Client, "obs-2", "k3", "PrepareEquipment", "trace-5", fail: true);
    metrics = await server.Client.GetFromJsonAsync<JsonElement>("/api/metrics");
    Assert(metrics.GetProperty("failedTransitions").GetInt64() == 1, "metrics must count failed transitions");
    Assert(metrics.GetProperty("compensations").GetInt64() == 1, "metrics must count compensations");
}

static async Task<JsonElement> Send(HttpClient client, string process, string key, string eventName, string corr, bool fail = false)
{
    // Helper собирает одинаковый JSON события, чтобы в самих тестах были видны только сценарии.
    var response = await client.PostAsJsonAsync("/api/bookings/events", new
    {
        processKey = process,
        idempotencyKey = key,
        eventName,
        correlationId = corr,
        failStep = fail
    });
    var text = await response.Content.ReadAsStringAsync();
    Assert(response.IsSuccessStatusCode, text);
    return JsonSerializer.Deserialize<JsonElement>(text);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string FindRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Task4Framework.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new DirectoryNotFoundException("Solution root not found.");
}

sealed class ApiProcess : IDisposable
{
    private readonly Process _process;
    public HttpClient Client { get; }

    private ApiProcess(Process process, HttpClient client) => (_process, Client) = (process, client);

    public static async Task<ApiProcess> Start(string project, int port)
    {
        // Каждый тест поднимает отдельный API на своем порту, чтобы сценарии не мешали друг другу.
        var process = Process.Start(new ProcessStartInfo("dotnet", $"run --no-restore --project \"{project}\" --urls http://127.0.0.1:{port}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Cannot start API.");
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        // Сервер стартует не сразу, поэтому тест ждет простую проверку живости.
        for (var i = 0; i < 80; i++)
        {
            if (process.HasExited) throw new InvalidOperationException(await process.StandardError.ReadToEndAsync());
            try { if ((await client.GetAsync("/health/live")).IsSuccessStatusCode) return new(process, client); }
            catch { await Task.Delay(250); }
        }
        throw new TimeoutException("API did not start.");
    }

    public void Dispose()
    {
        Client.Dispose();
        if (_process.HasExited) return;

        // После теста обязательно останавливаем дочерний API, иначе порт останется занятым.
        _process.Kill(entireProcessTree: true);
        _process.Dispose();
    }
}
