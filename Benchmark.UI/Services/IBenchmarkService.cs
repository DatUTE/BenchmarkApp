/**
 * @file IBenchmarkService.cs
 * @brief Service interface for managing benchmark sessions.
 *
 * Abstracts the native interop layer so ViewModels depend only on this
 * interface, keeping them testable and platform-independent.
 */

using Benchmark.Models;

namespace Benchmark.UI.Services;

/// <summary>
/// Manages the lifecycle of a two-process benchmark session.
/// Emits <see cref="SnapshotsUpdated"/> after each sampling cycle so
/// that subscribers can update the UI reactively (Observer pattern).
/// </summary>
public interface IBenchmarkService
{
    /// <summary>Gets whether a benchmark session is currently active.</summary>
    bool IsRunning { get; }

    /// <summary>Gets the current session, or <c>null</c> if no session is active.</summary>
    BenchmarkSession? CurrentSession { get; }

    /// <summary>
    /// Raised after each successful sampling cycle.
    /// Handlers are invoked on a background thread; dispatch to the UI thread as needed.
    /// </summary>
    event EventHandler<SnapshotPair>? SnapshotsUpdated;

    /// <summary>
    /// Starts background monitoring for both processes.
    /// Creates a new <see cref="BenchmarkSession"/> and begins sampling.
    /// </summary>
    /// <param name="processA">The first process to monitor.</param>
    /// <param name="processB">The second process to monitor.</param>
    Task StartAsync(ProcessInfo processA, ProcessInfo processB);

    /// <summary>
    /// Stops monitoring and marks the current session as ended.
    /// </summary>
    Task StopAsync();

    /// <summary>Returns the most recently collected snapshot for Process A.</summary>
    MetricSnapshot? GetLatestSnapshotA();

    /// <summary>Returns the most recently collected snapshot for Process B.</summary>
    MetricSnapshot? GetLatestSnapshotB();
}

/// <summary>
/// A paired snapshot from both monitored processes, emitted together
/// to guarantee temporal alignment in the UI.
/// </summary>
public sealed record SnapshotPair(MetricSnapshot A, MetricSnapshot B);
