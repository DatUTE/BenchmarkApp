/**
 * @file NativeBenchmarkService.cs
 * @brief Managed facade over the native Benchmark.Core.dll API.
 *
 * Translates between native structs and managed model types, handles
 * resource lifetime via IDisposable, and presents a clean API for the
 * application layer (Benchmark.UI) to consume without any P/Invoke knowledge.
 *
 * Design pattern: Facade — wraps the flat C API behind a typed, managed surface.
 */

using Benchmark.Models;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace Benchmark.Interop;

/// <summary>
/// Managed wrapper for a single native benchmark session.
/// Owns the native <c>BenchmarkHandle</c> and releases it on disposal.
/// </summary>
public sealed class NativeBenchmarkService : IDisposable
{
    private nint  handle_;
    private bool  disposed_;

    /// <summary>
    /// Opens a native benchmark session for the specified process.
    /// </summary>
    /// <param name="processId">The OS process identifier to monitor.</param>
    /// <param name="intervalMs">Sampling interval in milliseconds.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the native engine cannot be created (e.g., access denied, PID not found).
    /// </exception>
    public NativeBenchmarkService(uint processId, uint intervalMs = 1000)
    {
        try
        {
            _ = Process.GetProcessById((int)processId);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"Process {processId} is not running. " +
                "For Chrome/Brave, click Refresh, reselect, and start right away — " +
                "the PID may have changed while the browser is still open.");
        }

        handle_ = NativeMethods.BenchmarkCreate(processId, intervalMs);
        if (handle_ == nint.Zero)
        {
            var hint = OperatingSystem.IsWindows() && !IsCurrentProcessElevated()
                ? " Run the app as administrator (right-click the .exe → Run as administrator), " +
                  "or select processes running at the same privilege level."
                : string.Empty;

            throw new InvalidOperationException(
                $"Failed to open process {processId} for monitoring.{hint}");
        }
    }

    // ── Session control ───────────────────────────────────────────────────────

    /// <summary>Starts background metric collection.</summary>
    /// <exception cref="InvalidOperationException">Thrown if the native engine fails to start.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed_, this);
        if (NativeMethods.BenchmarkStart(handle_) != 0)
            throw new InvalidOperationException("Failed to start the native benchmark engine.");
    }

    /// <summary>Stops background metric collection.</summary>
    public void Stop()
    {
        if (!disposed_ && handle_ != nint.Zero)
            NativeMethods.BenchmarkStop(handle_);
    }

    /// <summary>Resets accumulated statistics (averages, peaks).</summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed_, this);
        NativeMethods.BenchmarkReset(handle_);
    }

    // ── Data retrieval ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recently collected metrics snapshot, or <c>null</c>
    /// if no sample has been taken yet.
    /// </summary>
    public MetricSnapshot? GetLatestSnapshot()
    {
        if (disposed_ || handle_ == nint.Zero) return null;

        if (NativeMethods.BenchmarkGetSnapshot(handle_, out var native) != 0)
            return null;

        return MapToManaged(in native);
    }

    // ── Process discovery (static) ────────────────────────────────────────────

    /// <summary>
    /// Enumerates all currently running processes and returns their
    /// identity information.
    /// </summary>
    /// <returns>
    /// A list of <see cref="ProcessInfo"/> records, sorted by process name.
    /// Processes that cannot be queried (e.g., system processes) are
    /// included with placeholder names.
    /// </returns>
    public static IReadOnlyList<ProcessInfo> EnumerateProcesses()
    {
        const int MaxProcesses = 4096;
        var pids = new uint[MaxProcesses];

        if (NativeMethods.BenchmarkEnumerateProcesses(pids, MaxProcesses, out uint count) != 0)
            return [];

        var result    = new List<ProcessInfo>((int)count);
        var nameBuffer = new StringBuilder(260);

        for (uint i = 0; i < count; i++)
        {
            uint pid = pids[i];
            if (pid == 0) continue;

            nameBuffer.Clear();
            NativeMethods.BenchmarkGetProcessName(pid, nameBuffer, 260);

            string name = nameBuffer.Length > 0 ? nameBuffer.ToString() : $"<{pid}>";
            result.Add(new ProcessInfo { ProcessId = pid, Name = name });
        }

        return result
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!disposed_)
        {
            if (handle_ != nint.Zero)
            {
                NativeMethods.BenchmarkStop(handle_);
                NativeMethods.BenchmarkDestroy(handle_);
                handle_ = nint.Zero;
            }
            disposed_ = true;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    private static MetricSnapshot MapToManaged(in NativeMetricsSnapshot native)
    {
        // FILETIME (100-ns intervals since 1601-01-01 UTC) → DateTime
        var startTime = native.StartTimeUtc > 0
            ? DateTime.FromFileTimeUtc((long)native.StartTimeUtc)
            : DateTime.MinValue;

        return new MetricSnapshot
        {
            Timestamp           = DateTime.UtcNow,
            ProcessId           = native.ProcessId,
            CpuPercent          = native.CpuUsagePercent,
            AverageCpuPercent   = native.AverageCpuPercent,
            PeakCpuPercent      = native.PeakCpuPercent,
            WorkingSetBytes     = (long)native.WorkingSetBytes,
            PrivateBytes        = (long)native.PrivateBytes,
            PeakWorkingSetBytes = (long)native.PeakWorkingSetBytes,
            ThreadCount         = native.ThreadCount,
            HandleCount         = native.HandleCount,
            IoReadBytes         = (long)native.IoReadBytes,
            IoWriteBytes        = (long)native.IoWriteBytes,
            StartTimeUtc        = startTime,
            Uptime              = TimeSpan.FromSeconds(native.UptimeSeconds),
        };
    }
}
