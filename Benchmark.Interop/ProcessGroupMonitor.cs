/**
 * @file ProcessGroupMonitor.cs
 * @brief Aggregates metrics across all processes belonging to one application.
 *
 * Unlike a static list of PIDs, this monitor re-enumerates processes by
 * executable name on every sample tick. This handles apps like Chrome/Brave
 * that continuously spawn and kill renderer processes: stale PIDs are pruned,
 * new PIDs get their own engine within one second of appearing.
 *
 * Aggregation rules (applied to all currently-alive engines):
 *   CPU          : sum — total processor time of the app
 *   Memory       : sum — total physical / private bytes of the app
 *   Threads      : sum — total threads across all processes
 *   Handles      : sum — total handles across all processes
 *   I/O          : sum — cumulative bytes across all processes
 *   Uptime       : max — age of the oldest (main) process in the group
 *   ProcessId    : lowest PID among live processes — the main process
 */

using Benchmark.Models;

namespace Benchmark.Interop;

/// <summary>
/// Monitors every process with a given executable name and exposes a single
/// aggregated <see cref="MetricSnapshot"/>. The engine list is refreshed on
/// every sample so transient processes (renderer workers, etc.) are captured.
/// </summary>
public sealed class ProcessGroupMonitor : IDisposable
{
    private readonly string          appName_;
    private readonly object          lock_    = new();
    private readonly Dictionary<uint, NativeBenchmarkService> engines_ = new();
    private readonly Timer           refreshTimer_;
    private bool                     disposed_;
    private bool                     started_;

    /// <summary>
    /// Initialises with an optional set of seed PIDs. The engine list is kept
    /// up-to-date by <see cref="GetAggregatedSnapshot"/> regardless of the seeds.
    /// </summary>
    /// <param name="appName">Executable name (e.g. "brave.exe").</param>
    /// <param name="seedPids">
    ///   Initial PIDs used to prime the first sample. May be empty or stale;
    ///   the next tick will discover the true current list.
    /// </param>
    public ProcessGroupMonitor(string appName, IReadOnlyList<uint>? seedPids = null)
    {
        appName_ = appName;

        foreach (var pid in seedPids ?? [])
        {
            try { engines_[pid] = new NativeBenchmarkService(pid); }
            catch { /* inaccessible or already gone */ }
        }

        // Refresh PID list every 500 ms — independent of the 1 s sampling tick.
        // This ensures newly spawned processes (e.g. Brave renderers) are picked
        // up within half a second without delaying the metric snapshot.
        refreshTimer_ = new Timer(_ => RefreshEngines(), null,
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Gets the number of processes currently active in this group.</summary>
    public int ActiveCount { get { lock (lock_) return engines_.Count; } }

    // ── Session control ───────────────────────────────────────────────────────

    /// <summary>Starts background sampling on all current engines.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed_, this);
        lock (lock_)
        {
            started_ = true;
            foreach (var e in engines_.Values)
                TryStart(e);
        }
    }

    /// <summary>Stops background sampling on all engines.</summary>
    public void Stop()
    {
        lock (lock_)
        {
            started_ = false;
            foreach (var e in engines_.Values)
                e.Stop();
        }
    }

    // ── Data retrieval ────────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the live process list, then returns an aggregated snapshot
    /// summing metrics across all processes currently belonging to this app.
    /// Returns <c>null</c> if no live engine has produced a sample yet.
    /// </summary>
    public MetricSnapshot? GetAggregatedSnapshot()
    {
        if (disposed_) return null;

        List<MetricSnapshot> snapshots;
        lock (lock_)
        {
            snapshots = engines_.Values
                .Select(e => e.GetLatestSnapshot())
                .Where(s => s is not null)
                .Select(s => s!)
                .ToList();
        }

        if (snapshots.Count == 0) return null;

        var mainPid = snapshots.Min(s => s.ProcessId);

        return new MetricSnapshot
        {
            Timestamp              = DateTime.UtcNow,
            ProcessId              = mainPid,

            CpuPercent             = snapshots.Sum(s => s.CpuPercent),
            AverageCpuPercent      = snapshots.Sum(s => s.AverageCpuPercent),
            PeakCpuPercent         = snapshots.Sum(s => s.PeakCpuPercent),

            WorkingSetBytes        = snapshots.Sum(s => s.WorkingSetBytes),
            PrivateBytes           = snapshots.Sum(s => s.PrivateBytes),
            PeakWorkingSetBytes    = snapshots.Sum(s => s.PeakWorkingSetBytes),
            PrivateWorkingSetBytes = snapshots.Sum(s => s.PrivateWorkingSetBytes),

            ThreadCount            = (uint)snapshots.Sum(s => (long)s.ThreadCount),
            HandleCount            = (uint)snapshots.Sum(s => (long)s.HandleCount),

            IoReadBytes            = snapshots.Sum(s => s.IoReadBytes),
            IoWriteBytes           = snapshots.Sum(s => s.IoWriteBytes),

            StartTimeUtc           = snapshots.Min(s => s.StartTimeUtc),
            Uptime                 = snapshots.Max(s => s.Uptime),
        };
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!disposed_)
        {
            disposed_ = true;
            refreshTimer_.Dispose();
            lock (lock_)
            {
                foreach (var e in engines_.Values)
                    e.Dispose();
                engines_.Clear();
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Compares the currently-running processes that match <see cref="appName_"/>
    /// against the active engine dictionary and:
    /// <list type="bullet">
    ///   <item>removes engines for processes that have exited,</item>
    ///   <item>creates and optionally starts engines for newly spawned processes.</item>
    /// </list>
    /// </summary>
    private void RefreshEngines()
    {
        // Enumerate live processes with matching name (runs on background thread)
        var livePids = NativeBenchmarkService
            .EnumerateProcesses()
            .Where(p => p.Name.Equals(appName_, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.ProcessId)
            .ToHashSet();

        lock (lock_)
        {
            // Prune engines for dead processes
            foreach (var pid in engines_.Keys.Where(k => !livePids.Contains(k)).ToList())
            {
                engines_[pid].Dispose();
                engines_.Remove(pid);
            }

            // Create engines for newly-appeared processes
            foreach (var pid in livePids.Where(p => !engines_.ContainsKey(p)))
            {
                try
                {
                    var engine = new NativeBenchmarkService(pid);
                    if (started_) TryStart(engine);
                    engines_[pid] = engine;
                }
                catch { /* process inaccessible — skip */ }
            }
        }
    }

    private static void TryStart(NativeBenchmarkService engine)
    {
        try { engine.Start(); }
        catch { /* ignore — engine will produce no samples */ }
    }
}
