using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Api.Auth;

public sealed record RegisterRequest(
    [Required, StringLength(80, MinimumLength = 2)] string DisplayName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 10)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);

public sealed record AccountProfile(string Id, string DisplayName, string Email, IList<string> Roles);

public sealed record AssigneeOption(string Id, string DisplayName, string Email);

public enum AccountDeletion { Deleted, NotFound, AdminProtected, Conflict }
