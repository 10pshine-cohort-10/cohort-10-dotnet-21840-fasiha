namespace TaskManagement.Api.Tasks;

public sealed class TaskProblem(int statusCode, string title) : Exception(title)
{
    public int StatusCode { get; } = statusCode;
}
