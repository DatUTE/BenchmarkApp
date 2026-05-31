/**
 * @file NativeMethods.cs
 * @brief P/Invoke declarations for Benchmark.Core.dll.
 *
 * Uses LibraryImport (source-generated P/Invoke, .NET 7+) instead of
 * DllImport for better AOT/trimming support. All entry points map directly
 * to the C exports declared in BenchmarkCore.h.
 *
 * This class is internal — consumers use NativeBenchmarkService instead.
 */

using System.Runtime.InteropServices;
using System.Text;

namespace Benchmark.Interop;

/// <summary>
/// Raw P/Invoke bindings to <c>Benchmark.Core.dll</c>.
/// Not intended for direct use outside of <see cref="NativeBenchmarkService"/>.
/// </summary>
[System.Security.SuppressUnmanagedCodeSecurity]
internal static partial class NativeMethods
{
    private const string DllName = "Benchmark.Core";

    // ── Session lifecycle ─────────────────────────────────────────────────

    /// <summary>Creates a new benchmark session for the given process.</summary>
    [LibraryImport(DllName, EntryPoint = "BenchmarkCreate")]
    internal static partial nint BenchmarkCreate(uint processId, uint intervalMs);

    /// <summary>Starts background sampling. Returns 0 on success.</summary>
    [LibraryImport(DllName, EntryPoint = "BenchmarkStart")]
    internal static partial int BenchmarkStart(nint handle);

    /// <summary>Stops background sampling (blocks until thread joins).</summary>
    [LibraryImport(DllName, EntryPoint = "BenchmarkStop")]
    internal static partial void BenchmarkStop(nint handle);

    /// <summary>Destroys the session and frees all native resources.</summary>
    [LibraryImport(DllName, EntryPoint = "BenchmarkDestroy")]
    internal static partial void BenchmarkDestroy(nint handle);

    // ── Data retrieval ────────────────────────────────────────────────────

    /// <summary>Retrieves the most recent metrics snapshot. Returns 0 on success.</summary>
    [LibraryImport(DllName, EntryPoint = "BenchmarkGetSnapshot")]
    internal static partial int BenchmarkGetSnapshot(
        nint handle,
        out NativeMetricsSnapshot snapshot);

    // ── Process enumeration ───────────────────────────────────────────────

    /// <summary>Enumerates running process IDs. Returns 0 on success.</summary>
    [LibraryImport(DllName, EntryPoint = "BenchmarkEnumerateProcesses")]
    internal static partial int BenchmarkEnumerateProcesses(
        [Out] uint[] outPids,
        uint         maxCount,
        out uint     outCount);

    /// <summary>
    /// Retrieves the executable base name for the given PID.
    /// Uses DllImport here because LibraryImport doesn't directly support
    /// passing a mutable StringBuilder as an ANSI output buffer.
    /// </summary>
    [DllImport(DllName, EntryPoint = "BenchmarkGetProcessName",
        CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int BenchmarkGetProcessName(
        uint          pid,
        StringBuilder outName,
        uint          bufferLen);

    // ── Utility ───────────────────────────────────────────────────────────

    /// <summary>Resets accumulated statistics for a session.</summary>
    [LibraryImport(DllName, EntryPoint = "BenchmarkReset")]
    internal static partial void BenchmarkReset(nint handle);
}
