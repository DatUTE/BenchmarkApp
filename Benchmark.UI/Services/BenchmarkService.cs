/**
 * @file BenchmarkService.cs
 * @brief Concrete implementation of IBenchmarkService.
 *
 * Supports two monitor strategies per slot:
 *   - Single-process : one NativeBenchmarkService (ungrouped selection)
 *   - App-group      : one ProcessGroupMonitor that aggregates N processes
 *
 * The strategy is chosen automatically based on ProcessInfo.AllProcessIds.
 */

using Benchmark.Interop;
using Benchmark.Models;

namespace Benchmark.UI.Services;

/// <summary>
/// Drives benchmark monitoring for one or two process slots, each of which may
/// be a single process or an entire app group (e.g., all Chrome instances).
/// Emits <see cref="SnapshotsUpdated"/> every second on a background timer.
/// </summary>
public sealed class BenchmarkService : IBenchmarkService, IDisposable
{
    // Each slot holds EITHER a NativeBenchmarkService OR a ProcessGroupMonitor.
    // Both implement Start/Stop/GetLatestSnapshot but have no common interface,
    // so we box them behind lightweight lambdas captured at start time.
    private Action?               startA_,  startB_;
    private Action?               stopA_,   stopB_;
    private Func<MetricSnapshot?> sampleA_ = static () => null;
    private Func<MetricSnapshot?> sampleB_ = static () => null;
    private IDisposable?          engineA_, engineB_;

    private BenchmarkSession? session_;
    private Timer?            timer_;
    private bool              disposed_;

    /// <inheritdoc/>
    public BenchmarkMode Mode { get; private set; }

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public BenchmarkSession? CurrentSession => session_;

    /// <inheritdoc/>
    public event EventHandler<SnapshotPair>? SnapshotsUpdated;

    // ── Session control ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task StartAsync(ProcessInfo processA, ProcessInfo? processB, BenchmarkMode mode)
    {
        if (IsRunning)
            await StopAsync();

        Mode = mode;

        // ── Slot A ──
        (engineA_, startA_, stopA_, sampleA_) = BuildSlot(processA);

        // ── Slot B ──
        if (processB is not null)
            (engineB_, startB_, stopB_, sampleB_) = BuildSlot(processB);
        else
            (engineB_, startB_, stopB_, sampleB_) = (null, null, null, () => null);

        session_ = new BenchmarkSession { ProcessA = processA, ProcessB = processB };

        startA_?.Invoke();
        startB_?.Invoke();
        IsRunning = true;

        timer_ = new Timer(OnSamplingTick, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        await (timer_?.DisposeAsync() ?? ValueTask.CompletedTask);
        timer_ = null;

        stopA_?.Invoke();   engineA_?.Dispose();   engineA_ = null;
        stopB_?.Invoke();   engineB_?.Dispose();   engineB_ = null;
        startA_ = startB_ = stopA_ = stopB_ = null;
        sampleA_ = sampleB_ = () => null;

        session_?.End();
        IsRunning = false;
    }

    // ── Data access ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public MetricSnapshot? GetLatestSnapshotA() => sampleA_?.Invoke();

    /// <inheritdoc/>
    public MetricSnapshot? GetLatestSnapshotB() => sampleB_?.Invoke();

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds the engine and its Start/Stop/Sample lambdas for one process slot.
    /// Uses <see cref="ProcessGroupMonitor"/> when the ProcessInfo carries multiple
    /// PIDs; falls back to <see cref="NativeBenchmarkService"/> for single-process.
    /// </summary>
    private static (IDisposable engine,
                    Action start, Action stop,
                    Func<MetricSnapshot?> sample)
        BuildSlot(ProcessInfo info)
    {
        if (info.AllProcessIds is { Count: > 1 } pids)
        {
            var grp = new ProcessGroupMonitor(info.Name, pids);
            return (grp, grp.Start, grp.Stop, grp.GetAggregatedSnapshot);
        }

        var svc = new NativeBenchmarkService(info.ProcessId);
        return (svc, svc.Start, svc.Stop, svc.GetLatestSnapshot);
    }

    private void OnSamplingTick(object? state)
    {
        var snapA = sampleA_?.Invoke();
        if (snapA is null) return;

        var snapB = sampleB_?.Invoke();

        session_?.AddSnapshotA(snapA);
        if (snapB is not null)
            session_?.AddSnapshotB(snapB);

        SnapshotsUpdated?.Invoke(this, new SnapshotPair(snapA, snapB));
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!disposed_)
        {
            StopAsync().GetAwaiter().GetResult();
            disposed_ = true;
        }
    }
}
