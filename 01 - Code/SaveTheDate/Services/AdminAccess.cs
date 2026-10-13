using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SaveTheDate.Data;

namespace SaveTheDate.Services;

public sealed class SuperAdminRequirement : IAuthorizationRequirement;

// Consult SQLite on every authorization, including sessions issued before revocation.
public sealed class SuperAdminHandler(IDbContextFactory<AppDbContext> factory, IOptions<IdentityOptions> options)
    : AuthorizationHandler<SuperAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SuperAdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.User.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        if (id is null || stamp is null) return;
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Id == id && u.SecurityStamp == stamp
            && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AdminAccess.Role))))
            context.Succeed(requirement);
    }
}

public sealed class AdminAccess(AuthenticationStateProvider authentication, IAuthorizationService authorization)
{
    public const string Role = "SuperAdmin";
    public const string Policy = "SuperAdministration";

    public async Task RequireAsync()
    {
        var state = await authentication.GetAuthenticationStateAsync();
        if (!(await authorization.AuthorizeAsync(state.User, Policy)).Succeeded)
            throw new UnauthorizedAccessException("Este espacio requiere acceso de superadministración.");
    }
}
