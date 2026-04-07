using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SampleManagement.Authentication;
/// <summary>
/// Used by HTTP to check authorization
/// </summary>
/// <param name="identityService">The implementation of identity service (injected from Program.cs)</param>
public class AutoAuthenticationHandler(
    IOptionsMonitor<AutoAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserIdentityService identityService) : AuthenticationHandler<AutoAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>
    /// Authenticates the current user
    /// </summary>
    /// <returns>Whether the user is authenticated</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        ClaimsPrincipal principal = await identityService.GetUserPrincipalAsync();

        if (principal.Identity?.IsAuthenticated == true)
        {
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.NoResult();
    }
}

public class AutoAuthenticationOptions : AuthenticationSchemeOptions
{
}

public static class AutoAuthenticationExtensions
{
    public static AuthenticationBuilder AddAutoAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AutoAuthenticationOptions, AutoAuthenticationHandler>(
            "AutoAuth", options => { });
    }
}
