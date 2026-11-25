using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Auth;

namespace TaskManagement.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
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
