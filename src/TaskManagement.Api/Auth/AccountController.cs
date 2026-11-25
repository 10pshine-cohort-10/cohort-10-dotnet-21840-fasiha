using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Data;

namespace TaskManagement.Api.Auth;

[ApiController]
[Authorize]
[Route("api/account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(
    AccountService accounts,
    UserManager<AppUser> users,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public object Csrf() => new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken };

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.RegisterAsync(request, cancellationToken);
        if (result.Succeeded)
            return StatusCode(StatusCodes.Status201Created);

        if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An account already exists for this email.");

        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Code, error.Description);
        return ValidationProblem(ModelState);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AccountProfile>> Login(LoginRequest request)
    {
        var profile = await accounts.LoginAsync(request);
        if (profile is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed. Check your credentials or try again later.");
        return profile;
    }

    [HttpGet("me")]
    public async Task<ActionResult<AccountProfile>> Me()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();
        return await accounts.ProfileAsync(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await accounts.LogoutAsync(users.GetUserId(User));
        return NoContent();
    }

    [HttpGet("assignees")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IReadOnlyList<AssigneeOption>> Assignees(
        [FromQuery, System.ComponentModel.DataAnnotations.StringLength(80)] string? search,
        CancellationToken cancellationToken)
    {
        var query = users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.DisplayName.Contains(term) || user.Email!.Contains(term));
        }

        return await query.OrderBy(user => user.DisplayName).ThenBy(user => user.Email)
            .Take(50)
            .Select(user => new AssigneeOption(user.Id, user.DisplayName, user.Email!))
            .ToListAsync(cancellationToken);
    }
}
