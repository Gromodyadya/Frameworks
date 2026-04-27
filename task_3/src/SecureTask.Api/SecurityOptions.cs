namespace SecureTask.Api;

public sealed record RateLimitOptions(int WindowSeconds, int DefaultLimit, int CreateItemLimit);

public sealed record SecurityOptions(
    string Mode,
    string[] TrustedOrigins,
    RateLimitOptions RateLimit)
{
    public bool IsProduction => Mode.Equals("Production", StringComparison.OrdinalIgnoreCase);

    public static SecurityOptions Load(string[] args)
    {
        // Здесь специально задается порядок источников: файл, потом env, потом аргументы.
        // Так проще проверить, какая настройка в итоге победила.
        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("TASK3__")
            .AddCommandLine(args)
            .Build();

        var origins = cfg.GetSection("TrustedOrigins").Get<string[]>() ?? [];
        var rate = cfg.GetSection("RateLimit").Get<RateLimitOptions>() ?? new(0, 0, 0);
        return new SecurityOptions(cfg["Mode"] ?? "", origins, rate).Validate();
    }

    private SecurityOptions Validate()
    {
        // Проверяю настройки до запуска сервера, чтобы приложение не стартовало с дырой.
        List<string> errors = [];
        if (!Mode.Equals("Training", StringComparison.OrdinalIgnoreCase) &&
            !Mode.Equals("Production", StringComparison.OrdinalIgnoreCase))
            errors.Add("Mode must be Training or Production.");
        if (TrustedOrigins.Length == 0)
            errors.Add("TrustedOrigins must contain at least one origin.");
        foreach (var origin in TrustedOrigins)
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") ||
                uri.AbsolutePath != "/" || !string.IsNullOrWhiteSpace(uri.Query))
                // Origin должен быть именно адресом источника, без пути и параметров.
                errors.Add($"Trusted origin '{origin}' must be an http/https origin without path.");
        if (RateLimit.WindowSeconds <= 0) errors.Add("RateLimit:WindowSeconds must be positive.");
        if (RateLimit.DefaultLimit <= 0) errors.Add("RateLimit:DefaultLimit must be positive.");
        if (RateLimit.CreateItemLimit <= 0) errors.Add("RateLimit:CreateItemLimit must be positive.");
        if (RateLimit.CreateItemLimit > RateLimit.DefaultLimit)
            errors.Add("RateLimit:CreateItemLimit must not exceed DefaultLimit.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
        return this;
    }
}
