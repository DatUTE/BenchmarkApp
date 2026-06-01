/**
 * @file NativeStructs.cs
 * @brief P/Invoke-compatible struct mirroring the native ProcessMetricsSnapshot.
 *
 * Layout (LayoutKind.Sequential, Pack=8) must exactly match the C++ struct
 * declared with #pragma pack(push, 8) in ProcessMetrics.h.
 * Field order and types must be identical on both sides.
 */

using System.Runtime.InteropServices;

namespace Benchmark.Interop;

/// <summary>
/// Managed mirror of the native <c>ProcessMetricsSnapshot</c> struct.
/// Used exclusively for P/Invoke marshaling — convert to
/// <see cref="Benchmark.Models.MetricSnapshot"/> via
/// <see cref="NativeBenchmarkService"/> before use in the application layer.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NativeMetricsSnapshot
{
    public uint   ProcessId;

    // CPU
    public double CpuUsagePercent;
    public double AverageCpuPercent;
    public double PeakCpuPercent;

    // Memory
    public ulong  WorkingSetBytes;
    public ulong  PrivateBytes;
    public ulong  PeakWorkingSetBytes;
    public ulong  PrivateWorkingSetBytes;

    // Threads & Handles
    public uint   ThreadCount;
    public uint   HandleCount;

    // Disk I/O
    public ulong  IoReadBytes;
    public ulong  IoWriteBytes;

    // Lifetime
    public ulong  StartTimeUtc;
    public ulong  UptimeSeconds;

    // Timing
    public ulong  SampleTimestamp;
}
