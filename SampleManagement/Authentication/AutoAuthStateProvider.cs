// <copyright file="AutoAuthStateProvider.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Authentication;

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>
/// Used by Blazor to check authorization.
/// </summary>
/// <param name="identityService">The implementation of identity service.</param>
public class AutoAuthStateProvider(IUserIdentityService identityService) : AuthenticationStateProvider
{
    /// <summary>
    /// The implementation of the identity service (injected from Program.cs).
    /// </summary>
    private readonly IUserIdentityService identityService = identityService;

    /// <summary>
    /// Wraps the ClaimsPrincipal (encoding the user's identity) in an AuthenticationState.
    /// </summary>
    /// <returns>The authentication state of the current user.</returns>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsPrincipal principal = await this.identityService.GetUserPrincipalAsync();
        return new AuthenticationState(principal);
    }
}
