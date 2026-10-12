using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SaveTheDate.Services;

public interface ICurrentUser { Task<string> GetIdAsync(); }

public class CurrentUser(AuthenticationStateProvider authentication) : ICurrentUser
{
    public async Task<string> GetIdAsync() => (await authentication.GetAuthenticationStateAsync()).User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Iniciá sesión para continuar.");
}
