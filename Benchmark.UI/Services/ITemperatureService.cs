/**
 * @file ITemperatureService.cs
 * @brief Interface for system hardware temperature monitoring.
 */

namespace Benchmark.UI.Services;

/// <summary>
/// Reads CPU and GPU temperatures from hardware sensors.
/// Temperatures are system-level metrics independent of which process is
/// being benchmarked — they provide thermal context during load testing.
/// </summary>
public interface ITemperatureService : IDisposable
{
    /// <summary>CPU package temperature in °C, or <c>null</c> if unavailable.</summary>
    float? CpuTemperature { get; }

    /// <summary>GPU core temperature in °C, or <c>null</c> if unavailable.</summary>
    float? GpuTemperature { get; }

    /// <summary>Display name of the detected GPU, or <c>null</c> if none found.</summary>
    string? GpuName { get; }

    /// <summary>
    /// Refreshes temperature readings from all sensors.
    /// Call once per sampling cycle; do not call on the UI thread.
    /// </summary>
    void Update();
}
