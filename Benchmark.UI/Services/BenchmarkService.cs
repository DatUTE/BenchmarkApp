/**
 * @file BenchmarkService.cs
 * @brief Concrete implementation of IBenchmarkService.
 *
 * Manages two NativeBenchmarkService instances (one per process), wires up
 * a System.Threading.Timer for the sampling tick, and raises SnapshotsUpdated
 * on each cycle. Implements the Observer-publisher side.
 */

using Benchmark.Interop;
using Benchmark.Models;

namespace Benchmark.UI.Services;

/// <summary>
/// Concrete benchmark service that drives two native engines in parallel,
/// collects paired snapshots, and stores them in the current session.
/// </summary>
public sealed class BenchmarkService : IBenchmarkService, IDisposable
{
    private NativeBenchmarkService? engineA_;
    private NativeBenchmarkService? engineB_;
    private BenchmarkSession?       session_;
    private Timer?                  timer_;
    private bool                    disposed_;

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public BenchmarkSession? CurrentSession => session_;

    /// <inheritdoc/>
    public event EventHandler<SnapshotPair>? SnapshotsUpdated;

    // ── Session control ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task StartAsync(ProcessInfo processA, ProcessInfo processB)
    {
        if (IsRunning)
            await StopAsync();

        engineA_ = new NativeBenchmarkService(processA.ProcessId);
        engineB_ = new NativeBenchmarkService(processB.ProcessId);

        session_ = new BenchmarkSession
        {
            ProcessA = processA,
            ProcessB = processB,
        };

        engineA_.Start();
        engineB_.Start();
        IsRunning = true;

        // Timer drives sampling tick — interval matches native engine (1 s).
        // The native engines sample on their own background threads; the timer
        // here just coordinates reading the latest value and raising the event.
        timer_ = new Timer(OnSamplingTick, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        await (timer_?.DisposeAsync() ?? ValueTask.CompletedTask);
        timer_ = null;

        engineA_?.Stop();
        engineB_?.Stop();
        engineA_?.Dispose();
        engineB_?.Dispose();
        engineA_ = null;
        engineB_ = null;

        session_?.End();
        IsRunning = false;
    }

    // ── Data access ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public MetricSnapshot? GetLatestSnapshotA() => engineA_?.GetLatestSnapshot();

    /// <inheritdoc/>
    public MetricSnapshot? GetLatestSnapshotB() => engineB_?.GetLatestSnapshot();

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnSamplingTick(object? state)
    {
        var snapA = GetLatestSnapshotA();
        var snapB = GetLatestSnapshotB();

        if (snapA is null || snapB is null) return;

        session_?.AddSnapshotA(snapA);
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
