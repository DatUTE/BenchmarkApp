/**
 * @file MetricSnapshot.cs
 * @brief Immutable record of all performance metrics at one point in time.
 */

namespace Benchmark.Models;

/// <summary>
/// An immutable snapshot of all monitored metrics for a single process,
/// collected at a specific point in time. Mirrors the native
/// <c>ProcessMetricsSnapshot</c> struct from Benchmark.Core.
/// </summary>
public sealed record MetricSnapshot
{
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>UTC time when this sample was collected by the managed layer.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>OS process identifier.</summary>
    public uint ProcessId { get; init; }

    // ── CPU ──────────────────────────────────────────────────────────────────

    /// <summary>Instantaneous CPU usage, [0, 100]%.</summary>
    public double CpuPercent { get; init; }

    /// <summary>Running mean CPU usage since monitoring started.</summary>
    public double AverageCpuPercent { get; init; }

    /// <summary>Maximum CPU usage observed since monitoring started.</summary>
    public double PeakCpuPercent { get; init; }

    // ── Memory ───────────────────────────────────────────────────────────────

    /// <summary>Working set in bytes (physical RAM consumed).</summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>Private committed bytes.</summary>
    public long PrivateBytes { get; init; }

    /// <summary>Peak working set in bytes (since process start).</summary>
    public long PeakWorkingSetBytes { get; init; }

    /// <summary>
    /// Private working set in bytes — RAM consumed exclusively by this process,
    /// excluding shared pages (DLLs mapped by multiple processes).
    /// Matches the "Memory" column shown in Windows Task Manager.
    /// </summary>
    public long PrivateWorkingSetBytes { get; init; }

    // ── Threads & Handles ────────────────────────────────────────────────────

    /// <summary>Current thread count.</summary>
    public uint ThreadCount { get; init; }

    /// <summary>Current open handle count.</summary>
    public uint HandleCount { get; init; }

    // ── Disk I/O (cumulative) ────────────────────────────────────────────────

    /// <summary>Cumulative bytes read from disk since process start.</summary>
    public long IoReadBytes { get; init; }

    /// <summary>Cumulative bytes written to disk since process start.</summary>
    public long IoWriteBytes { get; init; }

    // ── Process Lifetime ─────────────────────────────────────────────────────

    /// <summary>UTC time when the process was created.</summary>
    public DateTime StartTimeUtc { get; init; }

    /// <summary>Time elapsed since the process was created.</summary>
    public TimeSpan Uptime { get; init; }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Working set formatted as a human-readable string (e.g., "512 MB").</summary>
    public string WorkingSetFormatted => FormatBytes(WorkingSetBytes);

    /// <summary>Private bytes formatted as a human-readable string.</summary>
    public string PrivateBytesFormatted => FormatBytes(PrivateBytes);

    /// <summary>Private working set formatted as a human-readable string.</summary>
    public string PrivateWorkingSetFormatted => FormatBytes(PrivateWorkingSetBytes);

    private static string FormatBytes(long bytes)
    {
        const long GB = 1024L * 1024 * 1024;
        const long MB = 1024L * 1024;
        const long KB = 1024L;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F1} GB",
            >= MB => $"{bytes / (double)MB:F1} MB",
            >= KB => $"{bytes / (double)KB:F1} KB",
            _     => $"{bytes} B",
        };
    }
}
