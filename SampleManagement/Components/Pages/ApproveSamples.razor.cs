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
}
