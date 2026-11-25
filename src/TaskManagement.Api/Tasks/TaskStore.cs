using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Data;

namespace TaskManagement.Api.Tasks;

public sealed class TaskStore(AppDbContext db)
{
    public async Task<TaskPage> ListAsync(TaskActor actor, TaskFilter filter, CancellationToken cancellationToken)
    {
        var query = VisibleTo(actor).AsNoTracking();
        if (filter.Status is { } status)
            query = query.Where(task => task.Status == status);
        if (filter.Priority is { } priority)
            query = query.Where(task => task.Priority == priority);
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim();
            query = query.Where(task => task.Category == category);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(task => task.Title.Contains(search)
                || (task.Description != null && task.Description.Contains(search))
                || (task.Category != null && task.Category.Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(task => task.UpdatedAt).ThenBy(task => task.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(task => new TaskSummary(task.Id, task.Title, task.Category, task.Status, task.Priority,
                task.DueDate, task.Assignee == null ? null : new TaskPerson(task.Assignee.Id, task.Assignee.DisplayName)))
            .ToListAsync(cancellationToken);
        return new(items, total, filter.Page, filter.PageSize);
    }

    public async Task<TaskCounts> CountsAsync(TaskActor actor, CancellationToken cancellationToken)
    {
        var counts = await VisibleTo(actor).GroupBy(task => task.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(result => result.Status, result => result.Count, cancellationToken);
        return new(counts.GetValueOrDefault(WorkStatus.Pending), counts.GetValueOrDefault(WorkStatus.InProgress),
            counts.GetValueOrDefault(WorkStatus.Completed));
    }

    public Task<WorkTask?> FindAsync(TaskActor actor, Guid id, CancellationToken cancellationToken) =>
        VisibleTo(actor).Include(task => task.Creator).Include(task => task.Assignee)
            .SingleOrDefaultAsync(task => task.Id == id, cancellationToken);

    public Task<AppUser?> FindUserAsync(string id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public void Add(WorkTask task) => db.Tasks.Add(task);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private IQueryable<WorkTask> VisibleTo(TaskActor actor)
    {
        // Keep the creator and assignee on existing tasks when their accounts are deactivated.
        var tasks = db.Tasks.IgnoreQueryFilters([AppDbContext.ActiveUsersFilter]);
        return actor.IsAdmin ? tasks : tasks.Where(task => task.CreatorId == actor.Id || task.AssigneeId == actor.Id);
    }
}
