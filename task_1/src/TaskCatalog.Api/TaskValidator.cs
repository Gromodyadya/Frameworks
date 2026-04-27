namespace TaskCatalog.Api;

public sealed class TaskValidator
{
    // Валидация вынесена отдельно, чтобы endpoint не превращался в набор if-ов.
    // Здесь собраны правила предметной области для создания учебной задачи.
    public void Validate(CreateStudyTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw BadRequest("Title is required.");
        if (request.Title.Trim().Length > 80)
            throw BadRequest("Title length must be 80 characters or less.");
        if (string.IsNullOrWhiteSpace(request.Course))
            throw BadRequest("Course is required.");
        if (request.Course.Trim().Length > 60)
            throw BadRequest("Course length must be 60 characters or less.");
        if (request.Difficulty is < 1 or > 5)
            throw BadRequest("Difficulty must be between 1 and 5.");
        if (request.Notes?.Length > 250)
            throw BadRequest("Notes length must be 250 characters or less.");
    }

    // Все ошибки проверки имеют один код, а конкретная причина лежит в сообщении.
    private static AppException BadRequest(string message) =>
        new(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message);
}
