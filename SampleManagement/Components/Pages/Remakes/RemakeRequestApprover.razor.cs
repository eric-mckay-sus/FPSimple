// <copyright file="RemakeRequestApprover.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages.Remakes;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SampleManagement.Components.Common;

/// <summary>
/// Code-behind for RemakeRequestApprover.razor.
/// </summary>
public partial class RemakeRequestApprover : TableManager<RemakeRequestText>
{
    private RemakeRequestText? pendingRequest;
    private bool isApproving;
    private string? approvalError;
    private int approverNum;

    /// <summary>
    /// Gets or sets the authentication state provider for accessing the current associate's number.
    /// </summary>
    [Inject]
    public AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    /// <summary>
    /// Gets or sets the dialog to show upon pressing the 'deny' button for a row.
    /// </summary>
    private protected DeleteDialog DeleteDialog { get; set; } = default!;

    /// <summary>
    /// When this page loads, set the sorting information, then let the parent set up.
    /// </summary>
    /// <returns>A Task representing that the page has loaded.</returns>
    protected override async Task OnInitializedAsync()
    {
        this.CurrentSortColumn = "RequestTime";
        this.SortDir = "descending";
        AuthenticationState authState = await this.AuthStateProvider.GetAuthenticationStateAsync();

        // Could check the auth role here, but that's done page-side
        // We do, however, need the approver number for the SP.
        string? numClaim = authState.User.FindFirst("AssociateNum")?.Value;
        if (int.TryParse(numClaim, out int parsed))
        {
            this.approverNum = parsed;
        }

        await base.OnInitializedAsync();
    }

    private void HandleApprove(RemakeRequestText request)
    {
        if (request.Equals(this.pendingRequest))
        {
            this.CancelApproval();
            return;
        }

        this.pendingRequest = request;
        this.approvalError = null;
    }

    private void CancelApproval()
    {
        this.pendingRequest = null;
        this.approvalError = null;
    }

    private async Task ConfirmApproval()
    {
        if (this.pendingRequest == null)
        {
            return;
        }

        this.isApproving = true;
        this.approvalError = null;

        try
        {
            using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC [dbo].[ApproveRemake]
                    @sampleID    = {this.pendingRequest.SampleId},
                    @approverNum = {this.approverNum}");

            await this.RefreshData();
            this.ToastService.Notify(new (ToastType.Success, $"Sample #{this.pendingRequest.SampleId} approved for remake!"));
            this.pendingRequest = null;
        }
        catch (Exception ex)
        {
            this.approvalError = $"Database error: {ex.Message}";
            this.ToastService.Notify(new (ToastType.Danger, "Remake approval failed."));
        }
        finally
        {
            this.isApproving = false;
        }
    }

    /// <summary>
    /// Shows the delete dialog, and if confirmed, remove from underlying table in the DB (then update view).
    /// </summary>
    /// <param name="request">The sample to deny.</param>
    /// <returns>A Task representing that <paramref name="request"/> has been removed and the view has been updated.</returns>
    private async Task HandleDeny(RemakeRequestText request)
    {
        if (await this.DeleteDialog.ConfirmAsync(request))
        {
            // If the remake being denied was pending approval, close the approval window
            if (request.Equals(this.pendingRequest))
            {
                this.CancelApproval();
            }

            using FPSampleDbContext context = this.DbFactory.CreateDbContext();
            await context.RemakeRequests.Where(x => x.SampleId == request.SampleId).ExecuteDeleteAsync();
            await this.RefreshData();
            this.ToastService.Notify(new (ToastType.Success, $"Successfully denied remake for sample #{request.SampleId}"));
        }
    }
}
