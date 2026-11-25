using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Tasks;

namespace TaskManagement.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<WorkTask> Tasks => Set<WorkTask>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<WorkTask>(task =>
        {
            task.Property(item => item.Title).HasMaxLength(120).IsRequired();
            task.Property(item => item.Description).HasMaxLength(2000);
            task.Property(item => item.Category).HasMaxLength(60);
            task.Property(item => item.Version).IsConcurrencyToken();
            task.Property(item => item.CreatedAt).HasConversion(
                value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
            task.Property(item => item.UpdatedAt).HasConversion(
                value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
            task.HasOne(item => item.Creator).WithMany().HasForeignKey(item => item.CreatorId).OnDelete(DeleteBehavior.Restrict);
            task.HasOne(item => item.Assignee).WithMany().HasForeignKey(item => item.AssigneeId).OnDelete(DeleteBehavior.Restrict);
            task.HasIndex(item => new { item.UpdatedAt, item.Id }).IsDescending(true, false);
        });
        builder.Entity<AppUser>().Property(user => user.DisplayName).HasMaxLength(80).IsRequired();
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "ffbcfe34-ea3a-49bb-b54a-a974561064b9",
                Name = RoleNames.Admin,
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "admin-initial"
            },
            new IdentityRole
            {
                Id = "13b17f93-f0d0-4b7e-90a1-71a3dabd6bd0",
                Name = RoleNames.Member,
                NormalizedName = "MEMBER",
                ConcurrencyStamp = "member-initial"
            });
    }
}
