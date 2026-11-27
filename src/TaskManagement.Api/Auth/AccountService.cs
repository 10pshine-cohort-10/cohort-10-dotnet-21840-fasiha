using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Data;

namespace TaskManagement.Api.Auth;

public sealed class AccountService(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    AppDbContext db,
    ILogger<AccountService> logger)
{
    public async Task<IdentityResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName.Trim();
        if (displayName.Length < 2)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DisplayName",
                Description = "Enter a display name with at least two characters."
            });

        var email = request.Email.Trim();
        if (await FindAccountIncludingDeletedAsync(email, cancellationToken) is not null)
            return DuplicateAccount();

        var user = new AppUser { Email = email, UserName = email, DisplayName = displayName };
        try
        {
            var result = await CreateWithRoleAsync(user, request.Password, RoleNames.User, cancellationToken);
            if (result.Succeeded)
                logger.LogInformation("User account {UserId} registered", user.Id);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return DuplicateAccount();
        }
    }

    public async Task<AccountProfile?> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return null;

        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            logger.LogWarning("Sign-in rejected for account {UserId}", user.Id);
            return null;
        }

        logger.LogInformation("Account {UserId} signed in", user.Id);
        return await ProfileAsync(user);
    }

    public async Task<AccountProfile> ProfileAsync(AppUser user) =>
        new(user.Id, user.DisplayName, user.Email!, await users.GetRolesAsync(user));

    public async Task LogoutAsync(string? userId)
    {
        await signIn.SignOutAsync();
        logger.LogInformation("Account {UserId} signed out", userId);
    }

    public async Task<AccountDeletion> DeleteUserAsync(string id, string? administratorId)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
            return AccountDeletion.NotFound;

        if (await users.IsInRoleAsync(user, RoleNames.Admin))
            return AccountDeletion.AdminProtected;

        user.IsDeleted = true;
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
            return AccountDeletion.Conflict;

        logger.LogInformation("Administrator {AdminId} soft-deleted account {UserId}", administratorId, id);
        return AccountDeletion.Deleted;
    }

    public async Task ProvisionAdminAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        email = email.Trim();
        displayName = displayName.Trim();
        if (email.Length > 256 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)
            || displayName.Length is < 2 or > 80 || password.Length is < 10 or > 128)
            throw new InvalidOperationException("Provide a valid admin email, name, and password (10–128 characters).");

        var existing = await FindAccountIncludingDeletedAsync(email, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted)
                throw new InvalidOperationException("The requested admin email belongs to a deactivated account.");

            if (!await users.IsInRoleAsync(existing, RoleNames.Admin))
                throw new InvalidOperationException("The requested admin email already belongs to a user account.");
            logger.LogInformation("Administrator {UserId} already exists", existing.Id);
            return;
        }

        var admin = new AppUser { Email = email, UserName = email, DisplayName = displayName };
        var result = await CreateWithRoleAsync(admin, password, RoleNames.Admin, cancellationToken);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Admin provisioning failed: {string.Join(", ", result.Errors.Select(error => error.Description))}");
        logger.LogInformation("Administrator {UserId} provisioned", admin.Id);
    }

    private Task<AppUser?> FindAccountIncludingDeletedAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = users.NormalizeEmail(email);
        return db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(
            user => user.NormalizedUserName == normalizedEmail, cancellationToken);
    }

    private static IdentityResult DuplicateAccount() =>
        IdentityResult.Failed(new IdentityError
        {
            Code = "DuplicateEmail",
            Description = "An account already exists for this email."
        });

    private async Task<IdentityResult> CreateWithRoleAsync(
        AppUser user, string password, string role, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            return result;

        result = await users.AddToRoleAsync(user, role);
        if (result.Succeeded)
            await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
