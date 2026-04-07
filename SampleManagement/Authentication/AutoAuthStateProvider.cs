using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SampleManagement.Authentication;
/// <summary>
/// Used by Blazor to check authorization
/// </summary>
/// <param name="identityService">The implementation of identity service (injected from Program.cs)</param>
public class AutoAuthStateProvider(IUserIdentityService identityService) : AuthenticationStateProvider
{
    /// <summary>
    /// Wraps the ClaimsPrincipal (encoding the user's identity) in an AuthenticationState
    /// </summary>
    /// <returns>The authentication state of the current user</returns>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsPrincipal principal = await identityService.GetUserPrincipalAsync();
        return new AuthenticationState(principal);
    }
}
