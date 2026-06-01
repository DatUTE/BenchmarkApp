/**
 * @file IBenchmarkService.cs
 * @brief Service interface for managing benchmark sessions.
 *
 * Abstracts the native interop layer so ViewModels depend only on this
 * interface, keeping them testable and platform-independent.
 */

using Benchmark.Models;

namespace Benchmark.UI.Services;

/// <summary>Determines whether one or two processes are benchmarked.</summary>
public enum BenchmarkMode
{
    /// <summary>Monitor a single process; no Process B.</summary>
    Single,

    /// <summary>Monitor two processes side-by-side for comparison.</summary>
    Compare,
}

/// <summary>
/// Manages the lifecycle of a two-process benchmark session.
/// Emits <see cref="SnapshotsUpdated"/> after each sampling cycle so
/// that subscribers can update the UI reactively (Observer pattern).
/// </summary>
public interface IBenchmarkService
{
    /// <summary>Gets the active benchmark mode.</summary>
    BenchmarkMode Mode { get; }

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
    /// Starts background monitoring. In <see cref="BenchmarkMode.Compare"/> mode
    /// both processes are required; in <see cref="BenchmarkMode.Single"/> mode
    /// <paramref name="processB"/> must be <c>null</c>.
    /// </summary>
    Task StartAsync(ProcessInfo processA, ProcessInfo? processB, BenchmarkMode mode);

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
/// A paired snapshot emitted after each sampling cycle.
/// <see cref="B"/> is <c>null</c> in <see cref="BenchmarkMode.Single"/> sessions.
/// </summary>
public sealed record SnapshotPair(MetricSnapshot A, MetricSnapshot? B);
