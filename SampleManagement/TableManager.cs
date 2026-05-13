// <copyright file="TableManager.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement;

using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using ToastService = BlazorBootstrap.ToastService;
using System.Linq.Dynamic.Core;

using InterProcessIO;

/// <summary>
/// Minimal table logic for loading and paging data from <see cref="FPSampleDbContext"/>.
/// Designed to provide the data needed by <see cref="Components.Common.UniversalTable{T}"/> for display.
/// </summary>
/// <typeparam name="T">The EF entity type to load.</typeparam>
public class TableManager<T> : ComponentBase
    where T : class
{
    /// <summary>
    /// The maximum allowable sorts active at once.
    /// </summary>
    private static readonly byte MaxSorts = 2;

    /// <summary>
    /// Gets or sets this upload page's input provider.
    /// </summary>
    [Inject]
    public BlazorInputProvider InputProvider { get; set; } = default!;

    /// <summary>
    /// Gets or sets this upload page's output provider.
    /// </summary>
    [Inject]
    public BlazorReporter Reporter { get; set; } = default!;

    /// <summary>
    /// Gets the data from the DB table with rows of type <typeparamref name="T" /> (there should only be one).
    /// </summary>
    public List<T> DataView { get; private set; } = [];

    /// <summary>
    /// Gets a value indicating whether the table is loading.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Gets the message to show when a query returns no results.
    /// </summary>
    public virtual string EmptyMessage { get; } = "No data found. Please refresh.";

    /// <summary>
    /// Gets the current page number (always clamped between 1 and <see cref="TotalPages"/>, inclusive).
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Gets or sets the number of rows on one page.
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Gets the total number of rows retrieved by the current query.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Gets the number of pages retrieved by the current query (total rows divided by page size).
    /// </summary>
    public int TotalPages => this.PageSize > 0 ? (int)Math.Ceiling((double)this.TotalCount / this.PageSize) : 1;

    /// <summary>
    /// Gets or sets the optional model filter.
    /// </summary>
    public Filter<string> ModelFilter { get; set; } = new ("Model", null);

    /// <summary>
    /// Gets or sets the optional line name filter.
    /// </summary>
    public Filter<string> LineFilter { get; set; } = new ("Line",  null);

    /// <summary>
    /// Gets the list of sorts to be applied to the query.
    /// </summary>
    protected List<Sort> SortList { get; } = [];

    /// <summary>
    /// Gets or sets the navigation manager used to jump between pages.
    /// </summary>
    [Inject]
    private protected NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// Gets or sets the thread-safe DB context generator.
    /// </summary>
    [Inject]
    private protected IDbContextFactory<FPSampleDbContext> DbFactory { get; set; } = default!;

    /// <summary>
    /// Gets or sets the toast service for displaying success/failure messages.
    /// </summary>
    [Inject]
    private protected ToastService ToastService { get; set; } = default!;

    /// <summary>
    /// Loads the current page of data from the database.
    /// </summary>
    /// <param name="keepPage">Whether to keep the current page number.</param>
    /// <returns>The task for the load operation.</returns>
    public virtual async Task RefreshData(bool keepPage = false)
    {
        if (!keepPage)
        {
            this.CurrentPage = 1;
        }

        this.IsLoading = true;
        await this.InvokeAsync(this.StateHasChanged); // Show the loading spinner

        try
        {
            using FPSampleDbContext context = await this.DbFactory.CreateDbContextAsync();
            IQueryable<T> query = context.Set<T>().AsNoTracking();

            query = this.ApplyFilters(query);
            query = this.ApplySorting(query);
            this.TotalCount = await query.CountAsync();
            this.DataView = await query
                .Skip((this.CurrentPage - 1) * this.PageSize)
                .Take(this.PageSize)
                .ToDynamicListAsync<T>();
        }
        finally
        {
            this.IsLoading = false;
            await this.InvokeAsync(this.StateHasChanged); // Necessary to bookend previous InvokeAsync (otherwise it appears to load perpetually)
        }
    }

    /// <summary>
    /// Cycle order: None -> Asc -> Desc. All sort columns are "drive to zero". Toggling an already-sorted column simply follows the cycle.
    /// When a column is toggled to <see cref="SortDir.None"/>, the sort is removed from the list, freeing up a sort slot and promoting all lesser sorts.
    /// For example, toggling column B with the sort list [{col A, asc}, {col B, desc}, {col C, asc}] promotes col C and leaves col A unaffected, resulting in [{col A, asc}, {col C, asc}]
    /// The number of available sorts may be modified (to increase customization or to simplify) with <see cref="MaxSorts"/>.
    /// When toggling a new column, it is assigned the highest available sort priority. If there are no open sort slots, it overwrites the lowest priority sort.
    /// Sort priority is visualized with the subscript next to the sort arrow.
    /// </summary>
    /// <param name="columnName">The column to be toggled.</param>
    /// <returns>A Task representing that the sort has been applied.</returns>
    public async Task ToggleSort(string columnName)
    {
        // Determines if the column being toggled is already being sorted
        Sort? existingSort = this.SortList.FirstOrDefault(x => x.ColumnName == columnName);

        // If this column is already being sorted, cycle the state, removing if deactivated
        if (existingSort != null)
        {
            bool isActive = existingSort.Toggle();
            if (!isActive)
            {
                this.SortList.Remove(existingSort);
            }
        }

        // Otherwise, add it.
        else
        {
            Sort newSort = new (columnName, SortDir.Asc);

            // If there's an open slot, use it
            if (this.SortList.Count < MaxSorts)
            {
                this.SortList.Add(newSort);
            }

            // If not, overwrite the last sort
            else
            {
                this.SortList[^1] = newSort;
            }
        }

        await this.RefreshData();
    }

    /// <summary>
    /// Helper to render the arrow for <paramref name="columnName"/>.
    /// Denotes sort priority with the subscript attached to the arrow (primary sort gets no subscript).
    /// </summary>
    /// <param name="columnName">The column for which to update the sort icon.</param>
    /// <returns>The Unicode arrow representing the sort direction and sort priority.</returns>
    public string GetSortIcon(string columnName)
    {
        var sortEntry = this.SortList
            .Select((s, i) => new { s.ColumnName, s.Direction, Index = i })
            .FirstOrDefault(x => x.ColumnName == columnName);

        if (sortEntry == null || sortEntry.Direction == SortDir.None)
        {
            return "↕";
        }

        string arrow = sortEntry.Direction == SortDir.Asc ? "▲" : "▼";

        // Concatenate the arrow with its priority subscript. The primary sort gets no subscript.
        return arrow + GetSubscript(sortEntry.Index + 1);
    }

    /// <summary>
    /// Switches to a specific page and reloads data.
    /// </summary>
    /// <param name="newPage">The page to change to.</param>
    /// <returns>A Task representing that the page has been changed.</returns>
    public async Task ChangePage(int newPage)
    {
        if (newPage != this.CurrentPage && newPage >= 1 && newPage <= this.TotalPages)
        {
            this.CurrentPage = newPage;
            await this.RefreshData(keepPage: true);
        }
    }

    /// <summary>
    /// Changes the page size and reloads from the first page.
    /// </summary>
    /// <param name="newSize">The page size to change to.</param>
    /// <returns>A Task representing that the page size has been changed.</returns>
    public async Task AlterPageSize(int newSize)
    {
        if (newSize <= 0 || newSize == this.PageSize)
        {
            return;
        }

        this.PageSize = newSize;
        this.CurrentPage = 1;
        await this.RefreshData(keepPage: true);
    }

    /// <summary>
    /// Clears filters and reloads the table.
    /// </summary>
    /// <returns>A Task representing that the filters have been cleared.</returns>
    public async Task ClearAllFilters()
    {
        if (this.ModelFilter.IsActive || this.LineFilter.IsActive)
        {
            this.ModelFilter.Reset();
            this.LineFilter.Reset();

            await this.RefreshData();
            this.StateHasChanged();
        }
    }

    /// <summary>
    /// Clears the in-memory table state.
    /// </summary>
    /// <returns>A Task representing that the table has been totally reset.</returns>
    public async Task ClearData()
    {
        this.DataView.Clear();
        await this.ClearAllFilters();
        this.TotalCount = 0;
        this.CurrentPage = 1;
    }

    /// <summary>
    /// When the page loads, perform an initial load for the corresponding table.
    /// </summary>
    /// <returns>A Task representing that the page has loaded.</returns>
    protected override async Task OnInitializedAsync() => await this.RefreshData();

    /// <summary>
    /// Hook for children to apply filtering logic.
    /// Recommend applying model/line filters stored here using specific information about <typeparamref name="T"/>.
    /// </summary>
    /// <param name="query">The query to which filters should be appended.</param>
    /// <returns>An IQueryable object with filters applied.</returns>
    protected virtual IQueryable<T> ApplyFilters(IQueryable<T> query) => query;

    /// <summary>
    /// Uses dynamic LINQ to draft a SQL ORDER BY based on the current sort.
    /// </summary>
    /// <param name="query">The query to which the sorts should be appended.</param>
    /// <returns>An IQueryable object with sorts applied.</returns>
    protected IQueryable<T> ApplySorting(IQueryable<T> query)
    {
        // If there is no sort, simply order by itself (PK for DB objects)
        if (this.SortList.Count == 0)
        {
            return query.OrderBy(x => x);
        }

        bool isFirst = true;

        // Iterate through all the sorts and apply them in series
        foreach (Sort sort in this.SortList)
        {
            // Incomplete Sort objects shouldn't be in the list to begin with, but if they are, just ignore them
            if (string.IsNullOrEmpty(sort.ColumnName))
            {
                continue;
            }

            string sortExpression = $"{sort.ColumnName} {Sort.SortDirString[sort.Direction]}";

            // Ensure the first sort uses OrderBy instead of ThenBy, and throw flag to use ThenBy for remaining filters
            if (isFirst)
            {
                query = query.Where($"{sort.ColumnName} != null").OrderBy(sortExpression);
                isFirst = false;
            }
            else
            {
                query = ((IOrderedQueryable<T>)query).ThenBy(sortExpression);
            }
        }

        return query;
    }

    /// <summary>
    /// Converts an integer into Unicode subscript characters.
    /// </summary>
    private static string GetSubscript(int number)
    {
        // If it's the primary sort, we return empty to keep the UI clean
        if (number == 1)
        {
            return string.Empty;
        }

        char[] subscriptDigits = ['₀', '₁', '₂', '₃', '₄', '₅', '₆', '₇', '₈', '₉'];
        string result = string.Empty;

        foreach (char c in number.ToString())
        {
            result += subscriptDigits[c - '0'];
        }

        return result;
    }
}
