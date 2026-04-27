using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

var root = FindRoot();
var api = Path.Combine(root, "src", "SecureTask.Api", "SecureTask.Api.csproj");
var tests = new (string Name, Func<Task> Run)[]
{
    ("config priority uses args over env and file", ConfigPriority),
    ("invalid config stops startup", InvalidConfigStopsStartup),
    ("untrusted browser origin is blocked", UntrustedOriginBlocked),
    ("route limits are different", RouteLimitsDiffer),
    ("production mode hides internal details", ProductionHidesDetails)
};

foreach (var test in tests)
{
    await test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

async Task ConfigPriority()
{
    // Здесь env специально конфликтует с CLI, чтобы проверить заявленный приоритет.
    using var server = await ApiProcess.Start(api, 5301,
        ["TASK3__Mode=Production", "TASK3__RateLimit__DefaultLimit=3"],
        "--Mode=Training", "--RateLimit:DefaultLimit=9", "--RateLimit:CreateItemLimit=2");
    var cfg = await server.Client.GetFromJsonAsync<JsonElement>("/api/config");
    Assert(cfg.GetProperty("mode").GetString() == "Training", "args must override env mode");
    Assert(cfg.GetProperty("defaultLimit").GetInt32() == 9, "args must override env limit");
}

async Task InvalidConfigStopsStartup()
{
    // Неверный origin должен остановить приложение еще до начала приема запросов.
    using var server = await ApiProcess.StartExpectFailure(api, 5302, [], "--TrustedOrigins:0=http://good.local/path");
    Assert(server.Output.Contains("Trusted origin", StringComparison.OrdinalIgnoreCase), "startup error must explain origin problem");
}

async Task UntrustedOriginBlocked()
{
    using var server = await ApiProcess.Start(api, 5303, [], "--Mode=Training");
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
    request.Headers.Add("Origin", "http://evil.local");
    var response = await server.Client.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    Assert(response.StatusCode == HttpStatusCode.Forbidden, "untrusted origin must return 403");
    Assert(body.Contains("evil.local"), "training mode must include diagnostic details");
}

async Task RouteLimitsDiffer()
{
    // Для POST лимит ниже, поэтому второй запрос на создание уже должен получить 429.
    using var server = await ApiProcess.Start(api, 5304, [],
        "--RateLimit:DefaultLimit=4", "--RateLimit:CreateItemLimit=1");
    for (var i = 0; i < 4; i++)
        Assert((await server.Client.GetAsync("/api/items")).IsSuccessStatusCode, "GET should allow default limit");
    var first = await server.Client.PostAsJsonAsync("/api/items", new { title = "First" });
    var second = await server.Client.PostAsJsonAsync("/api/items", new { title = "Second" });
    Assert(first.IsSuccessStatusCode, "first POST should pass");
    Assert(second.StatusCode == (HttpStatusCode)429, "second POST should hit lower create limit");
}

async Task ProductionHidesDetails()
{
    using var server = await ApiProcess.Start(api, 5305, [], "--Mode=Production");
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
    request.Headers.Add("Origin", "http://evil.local");
    var response = await server.Client.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    Assert(response.StatusCode == HttpStatusCode.Forbidden, "production origin block must be 403");
    Assert(!body.Contains("evil.local"), "production response must hide origin details");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string FindRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new DirectoryNotFoundException("Solution root not found.");
}

sealed class ApiProcess : IDisposable
{
    private readonly Process? _process;
    public HttpClient Client { get; }
    public string Output { get; }

    private ApiProcess(Process? process, HttpClient client, string output = "")
        => (_process, Client, Output) = (process, client, output);

    public static async Task<ApiProcess> Start(string project, int port, string[] env, params string[] args)
    {
        var psi = BuildStartInfo(project, port, args);
        foreach (var item in env)
        {
            var parts = item.Split('=', 2);
            psi.Environment[parts[0]] = parts[1];
        }
        var process = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start API.");
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        // Сервер стартует не мгновенно, поэтому тест ждет готовности через /api/config.
        for (var i = 0; i < 80; i++)
        {
            if (process.HasExited) throw new InvalidOperationException(await process.StandardError.ReadToEndAsync());
            try { if ((await client.GetAsync("/api/config")).IsSuccessStatusCode) return new(process, client); }
            catch { await Task.Delay(250); }
        }
        throw new TimeoutException("API did not start.");
    }

    public static async Task<ApiProcess> StartExpectFailure(string project, int port, string[] env, params string[] args)
    {
        var psi = BuildStartInfo(project, port, args);
        foreach (var item in env)
        {
            var parts = item.Split('=', 2);
            psi.Environment[parts[0]] = parts[1];
        }
        var process = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start API.");
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        var output = await process.StandardError.ReadToEndAsync() + await process.StandardOutput.ReadToEndAsync();
        if (process.ExitCode == 0)
            throw new InvalidOperationException("invalid config must return non-zero exit code");
        return new(null, new HttpClient(), output);
    }

    private static ProcessStartInfo BuildStartInfo(string project, int port, string[] args)
    {
        var joined = string.Join(' ', args.Select(a => $"\"{a}\""));
        return new("dotnet", $"run --no-restore --project \"{project}\" --urls http://127.0.0.1:{port} -- {joined}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
    }

    public void Dispose()
    {
        Client.Dispose();
        if (_process is null || _process.HasExited) return;
        _process.Kill(entireProcessTree: true);
        _process.Dispose();
    }
}
