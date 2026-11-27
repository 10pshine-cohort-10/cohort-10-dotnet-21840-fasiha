using TaskManagement.Api.Data;

namespace TaskManagement.Api.Tasks;

public enum WorkStatus { Pending, InProgress, Completed }
public enum WorkPriority { Low, Medium, High }

public sealed class WorkTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public WorkStatus Status { get; set; }
    public WorkPriority Priority { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; }
    public required string CreatorId { get; init; }
    public required AppUser Creator { get; init; }
    public string? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }
}
