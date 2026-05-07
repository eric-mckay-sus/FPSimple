// <copyright file="AutoAuthenticationHandler.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Authentication;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

/// <summary>
/// Used by HTTP to check authorization.
/// </summary>
/// <param name="encoder">The URL encoder to be used (documentation for this type is terrible).</param>
/// <param name="logger">The logger to be used.</param>
/// <param name="options">An AuthenticationSchemeOptions object representing how the handler should be configured.</param>
/// <param name="identityService">The implementation of identity service (injected from Program.cs).</param>
public class AutoAuthenticationHandler(
    IOptionsMonitor<AutoAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserIdentityService identityService) : AuthenticationHandler<AutoAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>
    /// Authenticates the current user.
    /// </summary>
    /// <returns>Whether the user was successfully authenticated.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        ClaimsPrincipal principal = await identityService.GetUserPrincipalAsync();

        if (principal.Identity?.IsAuthenticated == true)
        {
            var ticket = new AuthenticationTicket(principal, this.Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.NoResult();
    }
}

/// <summary>
/// Empty options object or use in AutoAuthenticationHandler.
/// </summary>
public class AutoAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Contains extension methods for AutoAuthenticationHandler.
/// </summary>
public static class AutoAuthenticationExtensions
{
    /// <summary>
    /// Extension method to create an AuthenticationBuilder with auto-authentication.
    /// </summary>
    /// <param name="builder">The auth builder to which auto-authentication should be added.</param>
    /// <returns><paramref name="builder"/>, with auto-authentication enabled.</returns>
    public static AuthenticationBuilder AddAutoAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AutoAuthenticationOptions, AutoAuthenticationHandler>(
            "AutoAuth", options => { });
    }
}
