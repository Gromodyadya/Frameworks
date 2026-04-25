using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using Pr1.MinWebService.Domain;
using Pr1.MinWebService.Errors;
using Pr1.MinWebService.Middlewares;
using Pr1.MinWebService.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка сериализации, чтобы ответы были компактнее
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton<IItemRepository, InMemoryItemRepository>();

var app = builder.Build();

// Конвейер обработки запросов
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TimingAndLogMiddleware>();

// Точка доступа для чтения списка
app.MapGet("/api/items", (IItemRepository repo) =>
{
    return Results.Ok(repo.GetAll());
});

// Точка доступа для чтения по идентификатору
app.MapGet("/api/items/{id:guid}", (Guid id, IItemRepository repo) =>
{
    var item = repo.GetById(id);
    if (item is null)
        throw new NotFoundException("Элемент не найден");

    return Results.Ok(item);
});

// Точка доступа для создания
app.MapPost("/api/items", (HttpContext ctx, CreateItemRequest request, IItemRepository repo) =>
{
    // Правило 1: Название не пустое
    if (string.IsNullOrWhiteSpace(request.Title))
        throw new ValidationException("Название задачи не должно быть пустым");

    // Правило 2: Сложность от 1 до 10
    if (request.Difficulty < 1 || request.Difficulty > 10)
        throw new ValidationException("Сложность задачи должна быть от 1 до 10");

    var created = repo.Create(request.Title.Trim(), request.Difficulty);

    var location = $"/api/items/{created.Id}";
    ctx.Response.Headers.Location = location;

    return Results.Created(location, created);
});

app.Run();

public partial class Program { }
