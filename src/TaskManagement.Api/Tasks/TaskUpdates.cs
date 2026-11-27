using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Data;

namespace TaskManagement.Api.Tasks;

public sealed class TaskUpdates(
    IHubContext<TaskUpdatesHub, ITaskUpdatesClient> hub,
    AppDbContext db,
    ILogger<TaskUpdates> logger)
{
    public async Task PublishAsync(string creatorId, string? assigneeId = null, string? previousAssigneeId = null)
    {
        try
        {
            var affectedIds = new[] { creatorId, assigneeId, previousAssigneeId }.OfType<string>().Distinct().ToArray();
            var activeIds = await db.Users.Where(user => affectedIds.Contains(user.Id))
                .Select(user => user.Id).ToArrayAsync();
            var groups = activeIds.Select(TaskUpdatesHub.UserGroup).Append(TaskUpdatesHub.Administrators).ToArray();
            await hub.Clients.Groups(groups).TasksChanged();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Task changes were saved but the live notification failed");
        }
    }
}
