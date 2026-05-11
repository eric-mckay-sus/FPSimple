// <copyright file="Filter.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace SampleManagement;

/// <summary>
/// Container for the value and polarity of a filter.
/// </summary>
/// <typeparam name="T">One of string, int, or DateTime.</typeparam>
public class Filter<T> : IFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Filter{T}"/> class.
    /// Builds a filter using its key, value, and negation status (activity status is automatically determined).
    /// </summary>
    /// <param name="key">The name for the new filter.</param>
    /// <param name="value">The value for which to filter.</param>
    public Filter(string key, T? value)
    {
        this.Key = key;
        this.Value = value;
    }

    /// <summary>
    /// Gets or sets the name of this filter (self-identification).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets a value indicating whether this filter is being used in the current query (thus its value should be used). Automatically updated on value change.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets or sets this filter's value.
    /// </summary>
    public T? Value
    {
        get;
        set
        {
            field = value;
            this.IsActive = !IsDefault(value);
        }
    }

    /// <summary>
    /// Creates a deep copy of this filter.
    /// </summary>
    /// <returns>The deep copy.</returns>
    public IFilter Clone() => new Filter<T>(this.Key, this.Value);

    /// <summary>
    /// Gets the value of this filter as a nullable object.
    /// </summary>
    /// <returns>An object representing the generic type used by the value.</returns>
    public object? GetValue() => this.Value;

    /// <summary>
    /// Resets this filter to its default state.
    /// </summary>
    public void Reset()
    {
        this.Value = default!;
    }

    /// <summary>
    /// Gets a descriptive string of this filter.
    /// </summary>
    /// <returns>A string reporting this filter's key, value, and whether it is active.</returns>
    public override string ToString()
    {
        return $"{this.Key}: {this.Value} ({(!IsDefault(this.Value) ? "active" : "not active")})";
    }

    /// <summary>
    /// Determine if the user wishes to use this filter.
    /// </summary>
    /// <param name="val">The value to check against default.</param>
    /// <returns>Whether the value is its default (i.e. deactivated, and thus should not be used in a query).</returns>
    private static bool IsDefault(T? val) => val switch
    {
        string s => string.IsNullOrWhiteSpace(s), // Strings shouldn't be used if they're null OR whitespace
        _ when Equals(val, default(T)) => true, // All other datatypes only use null to denote default
        _ => false // Any value not covered by the above should be used in the query
    };
}

/// <summary>
/// Interface to bypass the complications of Filter's generic type.
/// </summary>
public interface IFilter
{
    /// <summary>
    /// Gets or sets the name of this filter (self-identification).
    /// </summary>
    string Key { get; set; }

    /// <summary>
    /// Gets a value indicating whether this filter is being used in the current query (thus its value should be used). Automatically updated on value change.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the internal value of this filter as a nullable object.
    /// </summary>
    /// <returns>A nullable object representing this filter's contents.</returns>
    object? GetValue();

    /// <summary>
    /// Creates a deep copy of this filter.
    /// </summary>
    /// <returns>The deep copy.</returns>
    IFilter Clone();

    /// <summary>
    /// Resets this filter to its default state.
    /// </summary>
    void Reset();
}
