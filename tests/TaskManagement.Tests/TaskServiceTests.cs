using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagement.Api.Data;
using TaskManagement.Api.Tasks;

namespace TaskManagement.Tests;

public sealed class TaskServiceTests
{
    [Theory]
    [InlineData("creator", false, true)]
    [InlineData("assigned", false, false)]
    [InlineData("any", true, true)]
    public void ManagementPolicyAllowsCreatorAndAdministrator(string userId, bool admin, bool expected)
    {
        var task = new WorkTask
        {
            CreatorId = "creator",
            Creator = new AppUser { Id = "creator" },
            AssigneeId = "assigned"
        };
        Assert.Equal(expected, new TaskActor(userId, admin).CanManage(task));
    }

    [Fact]
    public async Task StoreCombinesVisibilitySearchAndPagination()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var admin = new AppUser { DisplayName = "Administrator" };
        var member = new AppUser { DisplayName = "Member" };
        db.Users.AddRange(admin, member);
        await db.SaveChangesAsync();

        var store = new TaskStore(db);
        var service = new TaskService(store, TimeProvider.System, NullLogger<TaskService>.Instance);
        var adminActor = new TaskActor(admin.Id, true);
        var memberActor = new TaskActor(member.Id, false);
        var draft = new TaskInput { Title = "  Quarterly review  " };
        var hidden = await service.CreateAsync(adminActor, draft, CancellationToken.None);
        var owned = await service.CreateAsync(memberActor, draft, CancellationToken.None);
        var assigned = await service.CreateAsync(adminActor,
            new TaskInput { Title = "Quarterly results", AssigneeId = member.Id }, CancellationToken.None);

        Assert.Equal("Quarterly review", owned.Title);
        Assert.Equal(member.Id, owned.Assignee?.Id);
        var first = await store.ListAsync(memberActor,
            new TaskFilter { Search = "Quarterly", PageSize = 1 }, CancellationToken.None);
        var second = await store.ListAsync(memberActor,
            new TaskFilter { Search = "Quarterly", PageSize = 1, Page = 2 }, CancellationToken.None);
        Assert.Equal(2, first.Total);
        var visible = first.Items.Concat(second.Items).Select(task => task.Id).ToHashSet();
        Assert.True(visible.SetEquals([owned.Id, assigned.Id]));
        Assert.Equal(3, (await store.CountsAsync(adminActor, CancellationToken.None)).Pending);
        Assert.Null(await store.FindAsync(memberActor, hidden.Id, CancellationToken.None));

        var error = await Assert.ThrowsAsync<TaskProblem>(() => service.UpdateAsync(memberActor, assigned.Id,
            new EditTaskInput { Title = "Changed", Version = assigned.Version }, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }
}
