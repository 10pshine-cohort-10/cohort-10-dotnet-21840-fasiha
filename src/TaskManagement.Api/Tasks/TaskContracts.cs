using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Api.Tasks;

public class TaskInput
{
    [Required, StringLength(120)]
    public string Title { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [StringLength(60)]
    public string? Category { get; init; }

    [EnumDataType(typeof(WorkStatus))]
    public WorkStatus Status { get; init; } = WorkStatus.Pending;

    [EnumDataType(typeof(WorkPriority))]
    public WorkPriority Priority { get; init; } = WorkPriority.Medium;

    public DateOnly? DueDate { get; init; }

    [StringLength(450)]
    public string? AssigneeId { get; init; }
}

public sealed class EditTaskInput : TaskInput
{
    [Required]
    public Guid? Version { get; init; }
}

public sealed record ChangeStatusInput(
    [Required, EnumDataType(typeof(WorkStatus))] WorkStatus? Status,
    [Required] Guid? Version);

public sealed class TaskFilter
{
    [StringLength(120)]
    public string? Search { get; init; }

    [EnumDataType(typeof(WorkStatus))]
    public WorkStatus? Status { get; init; }

    [EnumDataType(typeof(WorkPriority))]
    public WorkPriority? Priority { get; init; }

    [StringLength(60)]
    public string? Category { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record TaskPerson(string Id, string DisplayName);
public sealed record TaskActor(string Id, bool IsAdmin)
{
    public bool CanManage(WorkTask task) => IsAdmin || task.CreatorId == Id;
}

public sealed record TaskSummary(Guid Id, string Title, string? Category, WorkStatus Status,
    WorkPriority Priority, DateOnly? DueDate, TaskPerson? Assignee);

public sealed record TaskDetail(Guid Id, string Title, string? Description, string? Category, WorkStatus Status,
    WorkPriority Priority, DateOnly? DueDate, TaskPerson Creator, TaskPerson? Assignee,
    DateTime CreatedAt, DateTime UpdatedAt, Guid Version, bool CanManage);

public sealed record TaskPage(IReadOnlyList<TaskSummary> Items, int Total, int Page, int PageSize);
public sealed record TaskCounts(int Pending, int InProgress, int Completed);
