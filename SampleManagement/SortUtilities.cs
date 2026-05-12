// <copyright file="SortUtilities.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement;

/// <summary>
/// Defines the valid sort directions as bytes for easy toggling.
/// </summary>
public enum SortDir : byte
{
    /// <summary>
    /// Apply no sort.
    /// </summary>
    None,

    /// <summary>
    /// Sort in ascending order.
    /// </summary>
    Asc,

    /// <summary>
    /// Sort in descending order.
    /// </summary>
    Desc,
}

/// <summary>
/// Contains the information identifying a sort, as well as a built-in toggle.
/// </summary>
public class Sort(string columnName, SortDir direction)
{
    /// <summary>
    /// Tracks the sort direction when toggled.
    /// </summary>
    private static readonly Dictionary<SortDir, SortDir> NextSortDir = new ()
    {
        [SortDir.None] = SortDir.Asc,
        [SortDir.Asc] = SortDir.Desc,
        [SortDir.Desc] = SortDir.None,
    };

    /// <summary>
    /// Gets the dictionary mapping sort directions to their strings.
    /// </summary>
    public static Dictionary<SortDir, string> SortDirString { get; } = new ()
    {
        [SortDir.None] = "none",
        [SortDir.Asc] = "asc",
        [SortDir.Desc] = "desc",
    };

    /// <summary>
    /// Gets or sets the name of the column by which to sort.
    /// </summary>
    public string? ColumnName { get; set; } = columnName;

    /// <summary>
    /// Gets or sets the direction by which to sort <see cref="ColumnName"/>.
    /// </summary>
    public SortDir Direction { get; set; } = direction;

    /// <summary>
    /// Toggles this <see cref="Sort"/>.
    /// </summary>
    /// <returns>Whether the sort is still active. If inactive, it should no longer be tracked.</returns>
    public bool Toggle()
    {
        this.Direction = NextSortDir[this.Direction];
        return this.Direction != SortDir.None;
    }
}
