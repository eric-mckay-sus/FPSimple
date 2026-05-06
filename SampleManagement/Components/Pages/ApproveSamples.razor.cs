// <copyright file="ApproveSamples.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Code-behind for the sample approval page.
/// </summary>
public partial class ApproveSamples : TableManager<UnapprovedSample>
{
    private UnapprovedSample? pendingSample;
    private DateOnly? expiryDate;
    private bool isApproving;
    private string? approvalError;
    private int approverNum;

    /// <summary>
    /// Gets or sets the authentication state provider for accessing the current associate's number.
    /// </summary>
    [Inject]
    public AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    /// <summary>
    /// When this page loads, set the sorting information, then let the parent set up.
    /// </summary>
    /// <returns>A Task representing that the page has loaded.</returns>
    protected override async Task OnInitializedAsync()
    {
        this.CurrentSortColumn = "CreationDate";
        this.SortDir = "descending";

        // Resolve once — auth state is cached in the identity service, so we can assume it is stable for this session
        AuthenticationState authState = await this.AuthStateProvider.GetAuthenticationStateAsync();
        string? numClaim = authState.User.FindFirst("AssociateNum")?.Value;
        if (int.TryParse(numClaim, out int parsed))
        {
            this.approverNum = parsed;
        }

        await base.OnInitializedAsync();
    }

    private void HandleApprove(UnapprovedSample sample)
    {
        if (sample.Equals(this.pendingSample))
        {
            this.CancelApproval();
            return;
        }

        this.pendingSample = sample;
        this.expiryDate = null;
        this.approvalError = null;
    }

    private void CancelApproval()
    {
        this.pendingSample = null;
        this.approvalError = null;
    }

    private async Task ConfirmApproval()
{
    if (this.pendingSample == null || this.expiryDate == default)
    {
        return;
    }

    this.isApproving = true;
    this.approvalError = null;

    try
    {
        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            EXEC [dbo].[ApproveSample]
                @sampleID    = {this.pendingSample.SampleID},
                @approverNum = {this.approverNum},
                @expiryDate  = {this.expiryDate}");

        this.pendingSample = null;
        await this.RefreshData();
        this.ToastService.Notify(new (ToastType.Success, "Sample approved successfully!"));
    }
    catch (Exception ex)
    {
        this.approvalError = $"Database error: {ex.Message}";
        this.ToastService.Notify(new (ToastType.Danger, "Approval failed."));
    }
    finally
    {
        this.isApproving = false;
    }
}
}
