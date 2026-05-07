// <copyright file="RemakeRequestCreator.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages.Remakes;

using BlazorBootstrap;
using Microsoft.EntityFrameworkCore;

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
        this.CurrentSortColumn = "CreationDate";
        this.SortDir = "descending";

        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
        this.availableReasons = await context.RemakeReasons.OrderBy(r => r.ReasonId).ToListAsync();

        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Excludes inactive samples and those already with pending remake requests.
    /// </summary>
    /// <param name="query"><inheritdoc/></param>
    /// <returns>The <paramref name="query"/>, with filters applied.</returns>
    protected override IQueryable<Sample> ApplyFilters(IQueryable<Sample> query)
        => query.Where(s => s.IsActive && !this.pendingRemakeIds.Contains(s.SampleId));

    /// <summary>
    /// Sets the pending sample to the selected row, expanding the form.
    /// If the same row is clicked again, the form collapses (toggle behavior matching ApproveSamples).
    /// </summary>
    /// <param name="sample">The sample for which a remake is being requested.</param>
    private void HandleRemake(Sample sample)
    {
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
