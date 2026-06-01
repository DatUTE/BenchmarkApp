/**
 * @file ProcessInfo.cs
 * @brief Immutable record representing a discovered OS process.
 */

namespace Benchmark.Models;

/// <summary>
/// Immutable snapshot of an OS process's identity information,
/// captured at the moment of discovery.
/// </summary>
public sealed record ProcessInfo
{
    /// <summary>Gets the OS process identifier.</summary>
    public required uint ProcessId { get; init; }

    /// <summary>Gets the executable base name (e.g., "chrome.exe").</summary>
    public required string Name { get; init; }

    /// <summary>Gets the full path to the executable, if available.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Number of running instances with the same executable name.
    /// Equals 1 for ungrouped entries.
    /// </summary>
    public int InstanceCount { get; init; } = 1;

    /// <summary>
    /// All process IDs belonging to this app group.
    /// <c>null</c> when this record represents a single, ungrouped process
    /// (in which case only <see cref="ProcessId"/> is relevant).
    /// </summary>
    public IReadOnlyList<uint>? AllProcessIds { get; init; }

    /// <summary>
    /// Optional subtitle override set by the listing layer (Group / Tree modes).
    /// When null, <see cref="DisplaySubtitle"/> falls back to the default PID string.
    /// </summary>
    public string? GroupDescription { get; init; }

    /// <summary>Display subtitle for the process picker list.</summary>
    public string DisplaySubtitle => GroupDescription ?? $"PID {ProcessId}";

    /// <inheritdoc/>
    public override string ToString() => $"{Name} (PID {ProcessId})";
}
