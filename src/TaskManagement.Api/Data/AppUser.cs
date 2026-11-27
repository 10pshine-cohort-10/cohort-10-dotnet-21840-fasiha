using Microsoft.AspNetCore.Identity;

namespace TaskManagement.Api.Data;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
