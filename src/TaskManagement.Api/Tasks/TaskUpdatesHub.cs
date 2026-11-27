using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskManagement.Api.Auth;

namespace TaskManagement.Api.Tasks;

public interface ITaskUpdatesClient
{
    Task TasksChanged();
}

[Authorize]
public sealed class TaskUpdatesHub : Hub<ITaskUpdatesClient>
{
    internal const string Administrators = "task-administrators";
    internal static string UserGroup(string id) => $"task-user:{id}";

    public override async Task OnConnectedAsync()
    {
        var group = Context.User?.IsInRole(RoleNames.Admin) == true
            ? Administrators
            : UserGroup(Context.UserIdentifier!);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        await Clients.Caller.TasksChanged();
    }
}
