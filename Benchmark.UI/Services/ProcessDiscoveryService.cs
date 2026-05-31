/**
 * @file ProcessDiscoveryService.cs
 * @brief Concrete implementation of IProcessDiscoveryService.
 *
 * Delegates native enumeration to NativeBenchmarkService.EnumerateProcesses()
 * and wraps System.Diagnostics.Process for launching executables.
 */

using Benchmark.Interop;
using Benchmark.Models;
using System.Diagnostics;

namespace Benchmark.UI.Services;

/// <summary>
/// Discovers running processes via the native C++ enumeration API,
/// and can launch executables to attach to them.
/// </summary>
public sealed class ProcessDiscoveryService : IProcessDiscoveryService
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<ProcessInfo>> GetRunningProcessesAsync()
    {
        // Offload to thread pool to avoid blocking the UI thread
        return Task.Run(NativeBenchmarkService.EnumerateProcesses);
    }

    /// <inheritdoc/>
    public async Task<ProcessInfo?> GetProcessFromExecutableAsync(string executablePath)
    {
        if (!File.Exists(executablePath))
            return null;

        return await Task.Run(() =>
        {
            try
            {
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName        = executablePath,
                    UseShellExecute = false,
                });

                if (proc is null) return null;

                // Give the process a moment to initialize
                proc.WaitForInputIdle(2000);

                return new ProcessInfo
                {
                    ProcessId      = (uint)proc.Id,
                    Name           = proc.ProcessName + ".exe",
                    ExecutablePath = executablePath,
                };
            }
            catch
            {
                return null;
            }
        });
    }

    /// <inheritdoc/>
    public ProcessInfo ResolveForMonitoring(ProcessInfo selected)
    {
        if (IsProcessRunning(selected.ProcessId))
            return selected;

        var resolved = FindByExecutableName(selected);
        if (resolved is not null)
            return resolved;

        throw new InvalidOperationException(
            $"{selected.Name} (PID {selected.ProcessId}) is no longer running. " +
            "Browsers use many short-lived processes — click Refresh, select again, then Start immediately.");
    }

    private static bool IsProcessRunning(uint processId)
    {
        try
        {
            using var proc = Process.GetProcessById((int)processId);
            return !proc.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ProcessInfo? FindByExecutableName(ProcessInfo selected)
    {
        var name = selected.Name;
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('<'))
            return null;

        var processName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(name)
            : name;

        Process[] matches;
        try
        {
            matches = Process.GetProcessesByName(processName);
        }
        catch
        {
            return null;
        }

        try
        {
            if (matches.Length == 0)
                return null;

            // Browser apps spawn many PIDs; the largest working set is usually the main process.
            var best = matches
                .OrderByDescending(p =>
                {
                    try { return p.WorkingSet64; }
                    catch { return 0L; }
                })
                .First();

            return new ProcessInfo
            {
                ProcessId      = (uint)best.Id,
                Name           = selected.Name,
                ExecutablePath = selected.ExecutablePath,
            };
        }
        finally
        {
            foreach (var p in matches)
                p.Dispose();
        }
    }
}
