// <copyright file="RemakeRequestApprover.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages.Remakes;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

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
    /// Gets the message to display when <see cref="TableManager{T}.DataView"/> is empty.
    /// </summary>
    public override string EmptyMessage => "No remakes pending approval matching these filters.";

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
        this.SortList.Add(new ("RequestTime", SortDir.Desc));
        AuthenticationState authState = await this.AuthStateProvider.GetAuthenticationStateAsync();

        // Could check the auth role here, but that's done page-side
        // We do, however, need the approver number for the SP.
        string? numClaim = authState.User.FindFirst("AssociateNum")?.Value;
        if (int.TryParse(numClaim, out int parsed))
        {
            this.approverNum = parsed;
        }

        await base.OnInitializedAsync();

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(new Uri(this.Navigation.Uri).Query);
        if (query.TryGetValue("sampleId", out StringValues raw) && int.TryParse(raw, out int sampleId))
        {
            await this.PreloadSampleAsync(sampleId);
        }
    }

    /// <summary>
    /// Filters out all already approved remake requests.
    /// Also applies model/line filters, if applicable.
    /// </summary>
    /// <param name="query"><inheritdoc/></param>
    /// <returns>The <paramref name="query"/>, with filters applied.</returns>
    protected override IQueryable<RemakeRequestText> ApplyFilters(IQueryable<RemakeRequestText> query)
    {
        query = query.Where(r => r.IsActive);

        if (this.ModelFilter.Value != null && this.ModelFilter.IsActive)
        {
            query = query.Where(x => x.Model.Contains(this.ModelFilter.Value));
        }

        if (this.LineFilter.Value != null && this.LineFilter.IsActive)
        {
            query = query.Where(x => x.Line.Contains(this.LineFilter.Value));
        }

        return query;
    }

    private async Task PreloadSampleAsync(int sampleId)
    {
        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
        RemakeRequestText? request = await context.ViewRemakeRequests
            .FirstOrDefaultAsync(r => r.SampleId == sampleId);

        if (request != null)
        {
            // Recreate the query
            IQueryable<RemakeRequestText> query = this.ApplyFilters(context.ViewRemakeRequests.AsQueryable());
            IQueryable<RemakeRequestText> sortedQuery = this.ApplySorting(query);

            // Fetch IDs to find the index
            int[] idList = await sortedQuery.Select(s => s.SampleId).ToArrayAsync();
            int index = Array.IndexOf(idList, sampleId);

            if (index != -1)
            {
                // Calculate and jump to the correct page
                int targetPage = (index / this.PageSize) + 1;
                if (this.CurrentPage != targetPage)
                {
                    await this.ChangePage(targetPage);
                }

                this.HandleApprove(request);
            }
        }
        else
        {
            this.Navigation.NavigateTo(this.Navigation.GetUriWithQueryParameter("sampleId", (string?)null));
            this.ToastService.Notify(new (ToastType.Warning, $"No pending remake request found for Sample #{sampleId}. It may have been already processed."));
        }
    }

    private void HandleApprove(RemakeRequestText request)
    {
        // Update the URL to match the sample being staged for approval
        this.Navigation.NavigateTo(this.Navigation.GetUriWithQueryParameter("sampleId", request.SampleId));
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

        // Clear the sample ID URL parameter
        this.Navigation.NavigateTo(this.Navigation.GetUriWithQueryParameter("sampleId", (string?)null));
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
    /// Shows the delete dialog, and if confirmed, deactivate in the underlying table in the DB (then update view).
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
            await context.RemakeRequests
                .Where(r => r.SampleId == request.SampleId)
                .ExecuteUpdateAsync(deactivate => deactivate
                    .SetProperty(r => r.IsActive, false));
            await this.RefreshData();
            this.ToastService.Notify(new (ToastType.Success, $"Successfully denied remake for sample #{request.SampleId}"));
        }
    }
}
