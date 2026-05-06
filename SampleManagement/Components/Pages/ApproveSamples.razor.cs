// <copyright file="ApproveSamples.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SampleManagement.Components.Common;

/// <summary>
/// Code-behind for the sample approval page.
/// </summary>
public partial class ApproveSamples : TableManager<Sample>
{
    private Sample? pendingSample;
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
    /// Gets or sets the dialog to show upon pressing the 'deny' button for a row.
    /// </summary>
    private protected DeleteDialog DeleteDialog { get; set; } = default!;

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

    /// <summary>
    /// Overrides refresh to filter out samples that already have an approver number.
    /// This ensures the filter is applied after every refresh (sort, page change, etc).
    /// </summary>
    /// <param name="query">The query to which filters should be applied.</param>
    /// <returns>A Task representing that <paramref name="query"/> is now filtered.</returns>
    protected override IQueryable<Sample> ApplyFilters(IQueryable<Sample> query)
        => query.Where(s => s.ApproverNum == null);

    private void HandleApprove(Sample sample)
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

            await this.RefreshData();
            this.ToastService.Notify(new (ToastType.Success, $"Sample #{this.pendingSample.SampleID} approved!"));
            this.pendingSample = null;
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

    /// <summary>
    /// Shows the delete dialog, and if confirmed, remove from underlying table in the DB (then update view).
    /// </summary>
    /// <param name="sample">The sample to deny.</param>
    /// <returns>A Task representing that <paramref name="sample"/> has been removed and the view has been updated.</returns>
    private async Task HandleDeny(Sample sample)
    {
        if (await this.DeleteDialog.ConfirmAsync(sample))
        {
            using FPSampleDbContext context = this.DbFactory.CreateDbContext();
            await context.Samples.Where(x => x.SampleID == sample.SampleID).ExecuteDeleteAsync();
            await this.RefreshData();
            this.ToastService.Notify(new (ToastType.Success, $"Successfully deleted sample #{sample.SampleID}"));
        }
    }
}
