// <copyright file="RemakeRequestCreator.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages.Remakes;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Code-behind for RemakeRequestCreator.razor.
/// Displays all active samples and allows non-approver associates to submit remake requests.
/// </summary>
public partial class RemakeRequestCreator : TableManager<Sample>
{
    /// <summary>
    /// The sample currently selected for a remake request, driving both form expansion and row highlight.
    /// </summary>
    private Sample? pendingSample;

    /// <summary>
    /// The form data collected from the remake request form.
    /// </summary>
    private RemakeFormData formData = new ();

    /// <summary>
    /// The list of available remake reasons, populated from the database on load.
    /// </summary>
    private IList<RemakeReason> availableReasons = [];

    private HashSet<int> pendingRemakeIds = [];

    /// <summary>
    /// Whether the form submission is currently in progress, used to disable buttons and show a spinner.
    /// </summary>
    private bool isSubmitting;

    /// <summary>
    /// The error message to display beneath the form inputs on a failed submission.
    /// </summary>
    private string? errorMessage;

    /// <summary>
    /// Gets the message to display when <see cref="TableManager{T}.DataView"/> is empty.
    /// </summary>
    public override string EmptyMessage => "No samples matching these filters available for remake request.";

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="keepPage">Whether to keep the page value (or reset it to 1).</param>
    /// <returns>A Task representing that the view has been refreshed to reflect the model.</returns>
    public override async Task RefreshData(bool keepPage = false)
    {
        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
        this.pendingRemakeIds = await context.RemakeRequests
            .Select(r => r.SampleId)
            .ToHashSetAsync();

        await base.RefreshData(keepPage);
    }

    /// <summary>
    /// When this page loads, set the sorting information, then let the parent set up.
    /// </summary>
    /// <returns>A Task representing that the page has loaded.</returns>
    protected override async Task OnInitializedAsync()
    {
        this.SortList.Add(new ("CreationDate", SortDir.Desc));
        this.SortList.Add(new ("SampleId", SortDir.Desc));

        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
        this.availableReasons = await context.RemakeReasons.OrderBy(r => r.ReasonId).ToListAsync();

        await base.OnInitializedAsync();

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(new Uri(this.Navigation.Uri).Query);
        if (query.TryGetValue("sampleId", out StringValues raw) && int.TryParse(raw, out int sampleId))
        {
            await this.PreloadSampleAsync(sampleId);
        }
    }

    /// <summary>
    /// Excludes inactive samples and those already with pending remake requests.
    /// Also applies model/line filters, if applicable.
    /// </summary>
    /// <param name="query"><inheritdoc/></param>
    /// <returns>The <paramref name="query"/>, with filters applied.</returns>
    protected override IQueryable<Sample> ApplyFilters(IQueryable<Sample> query)
    {
        query = query.Where(s => s.IsActive && !this.pendingRemakeIds.Contains(s.SampleId));

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

    /// <summary>
    /// Verifies a sample ID and prompts for a remake if one is found.
    /// </summary>
    /// <param name="sampleId">The sample ID to verify and load.</param>
    /// <returns>A Task representing that the sample is ready.</returns>
    private async Task PreloadSampleAsync(int sampleId)
    {
        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
        Sample? sample = await context.Samples
            .FirstOrDefaultAsync(s => s.SampleId == sampleId);

        string? validationError = null;

        if (sample == null)
        {
            validationError = $"Sample #{sampleId} was not found.";
        }
        else if (!sample.IsActive)
        {
            validationError = $"Sample #{sampleId} is inactive and cannot be remade.";
        }
        else if (this.pendingRemakeIds.Contains(sampleId))
        {
            validationError = $"Sample #{sampleId} already has a pending remake request.";
        }
        else
        {
            // Recreate the query
            IQueryable<Sample> query = this.ApplyFilters(context.Samples.AsQueryable());
            IQueryable<Sample> sortedQuery = this.ApplySorting(query);

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

                this.HandleRemake(sample);
            }
        }

        // If there was a navigation error, report it and amend the URL
        if (validationError != null)
        {
            this.Navigation.NavigateTo(this.Navigation.GetUriWithQueryParameter("sampleId", (string?)null));
            this.ToastService.Notify(new (ToastType.Warning, validationError));
        }
    }

    /// <summary>
    /// Sets the pending sample to the selected row, expanding the form.
    /// If the same row is clicked again, the form collapses (toggle behavior matching ApproveSamples).
    /// </summary>
    /// <param name="sample">The sample for which a remake is being requested.</param>
    private void HandleRemake(Sample sample)
    {
        // Update the URL to match the sample being staged for remake request
        this.Navigation.NavigateTo(this.Navigation.GetUriWithQueryParameter("sampleId", sample.SampleId));
        if (sample.Equals(this.pendingSample))
        {
            this.CancelRemake();
            return;
        }

        this.pendingSample = sample;
        this.formData = new ();
        this.errorMessage = null;
    }

    /// <summary>
    /// Clears the pending sample, collapsing the form and removing any error state.
    /// </summary>
    private void CancelRemake()
    {
        this.pendingSample = null;
        this.formData = new ();
        this.errorMessage = null;

        // Clear the sample ID URL parameter
        this.Navigation.NavigateTo(this.Navigation.GetUriWithQueryParameter("sampleId", (string?)null));
    }

    /// <summary>
    /// Submits the remake request to the database via the RequestRemake stored procedure.
    /// On success, toasts and resets. On failure, surfaces the DB error message in-form.
    /// </summary>
    /// <returns>A Task representing the completion of the submission attempt.</returns>
    private async Task HandleSubmit()
    {
        if (this.pendingSample == null)
        {
            return;
        }

        this.isSubmitting = true;
        this.errorMessage = null;

        try
        {
            using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC [dbo].[RequestRemake]
                    @sampleID    = {this.pendingSample.SampleId},
                    @requesterNum = {this.formData.AssociateNum},
                    @reasonID    = {this.formData.ReasonId}");

            await this.RefreshData();
            this.ToastService.Notify(new (ToastType.Success, $"Remake requested for sample #{this.pendingSample.SampleId}."));
            this.CancelRemake();
        }
        catch (Exception ex)
        {
            this.errorMessage = $"Database error: {ex.Message}";
            this.ToastService.Notify(new (ToastType.Danger, "Remake request failed."));
        }
        finally
        {
            this.isSubmitting = false;
        }
    }

    /// <summary>
    /// Holds the mutable fields collected by the remake request form.
    /// Sample ID and dummy sample number are read from <see cref="pendingSample"/> directly.
    /// </summary>
    public record RemakeFormData
    {
        /// <summary>
        /// Gets or sets the associate number entered by the requester.
        /// Validated as a real associate DB-side via the RequestRemake SP.
        /// </summary>
        public int? AssociateNum { get; set; }

        /// <summary>
        /// Gets or sets the selected remake reason ID.
        /// </summary>
        public byte? ReasonId { get; set; }
    }
}
