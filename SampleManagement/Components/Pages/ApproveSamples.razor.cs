// <copyright file="ApproveSamples.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages;

/// <summary>
/// Code-behind for the sample approval page.
/// </summary>
public partial class ApproveSamples : TableManager<UnapprovedSample>
{
    /// <summary>
    /// When this page loads, set the sorting information, then let the parent set up.
    /// </summary>
    /// <returns>A Task representing that the page has loaded.</returns>
    protected override async Task OnInitializedAsync()
    {
        this.CurrentSortColumn = "CreationDate";
        this.SortDir = "descending";
        await base.OnInitializedAsync();
    }

    private async Task HandleApprove(UnapprovedSample sample)
    {
        // Show confirmation window requesting expiry date
        using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();

        // Get associate number from UserPrincipal and sample ID from input sample
        // Execute SP ApproveSample with sample ID, approver associate number, and expiry date
        // Show success/failure toast (failure toast with error text)
    }
}
