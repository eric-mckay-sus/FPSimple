// <copyright file="RemakeRequestCreator.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement.Components.Pages.Remakes;

using SampleManagement;

/// <summary>
/// Code-behind for RemakeRequestCreator.razor.
/// </summary>
public partial class RemakeRequestCreator : TableManager<RemakeRequestText>
{
    /// <summary>
    /// When this page loads, set the sorting information, then let the parent set up.
    /// </summary>
    /// <returns>A Task representing that the page has loaded.</returns>
    protected override async Task OnInitializedAsync()
    {
        this.CurrentSortColumn = "RequestTime";
        this.SortDir = "descending";
        await base.OnInitializedAsync();
    }
}
