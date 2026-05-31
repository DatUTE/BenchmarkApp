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

    /// <inheritdoc/>
    public override string ToString() => $"{Name} (PID {ProcessId})";
}
