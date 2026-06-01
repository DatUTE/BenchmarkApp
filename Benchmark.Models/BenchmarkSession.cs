/**
 * @file BenchmarkSession.cs
 * @brief Aggregates all collected data for a single benchmark run.
 */

namespace Benchmark.Models;

/// <summary>
/// Aggregates all data collected during one benchmark session comparing
/// two processes. Acts as the Repository for metric snapshots during a run.
/// </summary>
public sealed class BenchmarkSession
{
    private readonly List<MetricSnapshot> snapshotsA_ = [];
    private readonly List<MetricSnapshot> snapshotsB_ = [];
    private readonly object lockA_ = new();
    private readonly object lockB_ = new();

    /// <summary>Gets the unique session identifier (generated at construction time).</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Gets the UTC time the session was created.</summary>
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    /// <summary>Gets the UTC time the session ended, or null if still running.</summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>Gets the process information for Application A.</summary>
    public required ProcessInfo ProcessA { get; init; }

    /// <summary>
    /// Gets the process information for Application B.
    /// <c>null</c> when the session runs in <see cref="BenchmarkMode.Single"/> mode.
    /// </summary>
    public ProcessInfo? ProcessB { get; init; }

    /// <summary>Gets a thread-safe snapshot of all collected metrics for Application A.</summary>
    public IReadOnlyList<MetricSnapshot> SnapshotsA
    {
        get { lock (lockA_) { return snapshotsA_.ToList(); } }
    }

    /// <summary>Gets a thread-safe snapshot of all collected metrics for Application B.</summary>
    public IReadOnlyList<MetricSnapshot> SnapshotsB
    {
        get { lock (lockB_) { return snapshotsB_.ToList(); } }
    }

    /// <summary>Returns the total duration of the session.</summary>
    public TimeSpan Duration => (EndedAt ?? DateTime.UtcNow) - StartedAt;

    /// <summary>Appends a new snapshot for Application A (thread-safe).</summary>
    public void AddSnapshotA(MetricSnapshot snapshot)
    {
        lock (lockA_) { snapshotsA_.Add(snapshot); }
    }

    /// <summary>Appends a new snapshot for Application B (thread-safe).</summary>
    public void AddSnapshotB(MetricSnapshot snapshot)
    {
        lock (lockB_) { snapshotsB_.Add(snapshot); }
    }

    /// <summary>Marks the session as ended at the current UTC time.</summary>
    public void End() => EndedAt = DateTime.UtcNow;
}
