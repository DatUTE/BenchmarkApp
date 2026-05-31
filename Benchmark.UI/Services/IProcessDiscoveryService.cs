/**
 * @file IProcessDiscoveryService.cs
 * @brief Service interface for OS process enumeration and lookup.
 */

using Benchmark.Models;

namespace Benchmark.UI.Services;

/// <summary>
/// Provides discovery of OS processes available for monitoring.
/// </summary>
public interface IProcessDiscoveryService
{
    /// <summary>
    /// Returns all currently running processes, sorted by name.
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> GetRunningProcessesAsync();

    /// <summary>
    /// Creates a <see cref="ProcessInfo"/> for the executable at the given path.
    /// Launches the process if it is not already running, or attaches to an
    /// existing instance if one is found.
    /// </summary>
    /// <param name="executablePath">Absolute path to the .exe file.</param>
    /// <returns>Process info, or <c>null</c> if the process could not be started.</returns>
    Task<ProcessInfo?> GetProcessFromExecutableAsync(string executablePath);

    /// <summary>
    /// Returns an up-to-date <see cref="ProcessInfo"/> for monitoring.
    /// If the original PID has exited (common for Chrome/Brave), finds a
    /// running process with the same executable name.
    /// </summary>
    ProcessInfo ResolveForMonitoring(ProcessInfo selected);
}
