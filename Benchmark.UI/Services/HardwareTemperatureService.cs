/**
 * @file HardwareTemperatureService.cs
 * @brief ITemperatureService implementation using LibreHardwareMonitorLib.
 *
 * LibreHardwareMonitorLib (the engine behind HWiNFO, OpenHardwareMonitor, etc.)
 * reads hardware sensors via a kernel-mode driver (WinRing0x64.sys).
 * Admin rights are required — satisfied by the existing app.manifest.
 *
 * Sensor selection:
 *   CPU — "CPU Package" sensor; falls back to first available CPU temperature.
 *   GPU — first temperature sensor on any detected GPU (NVIDIA / AMD / Intel Arc).
 *
 * If hardware access fails (VM, no driver, restricted environment) every
 * property returns null so the UI can display "N/A" gracefully.
 */

using LibreHardwareMonitor.Hardware;

namespace Benchmark.UI.Services;

/// <summary>
/// Reads CPU and GPU temperatures via LibreHardwareMonitorLib.
/// Thread-safe: <see cref="Update"/> may be called from any thread;
/// property reads are lock-free (volatile writes from Update side).
/// </summary>
public sealed class HardwareTemperatureService : ITemperatureService
{
    private readonly Computer computer_;
    // float.NaN is used as the "sensor unavailable" sentinel — volatile is
    // safe for float (32-bit, atomically readable on all .NET platforms).
    private volatile float   cpuTemperature_ = float.NaN;
    private volatile float   gpuTemperature_ = float.NaN;
    private volatile string? gpuName_;
    private bool             disposed_;

    /// <inheritdoc/>
    public float? CpuTemperature => float.IsNaN(cpuTemperature_) ? null : cpuTemperature_;

    /// <inheritdoc/>
    public float? GpuTemperature => float.IsNaN(gpuTemperature_) ? null : gpuTemperature_;

    /// <inheritdoc/>
    public string? GpuName => gpuName_;

    /// <summary>
    /// Opens hardware monitoring. Constructor is safe to call even if the
    /// required driver cannot be loaded — all sensors will simply return null.
    /// </summary>
    public HardwareTemperatureService()
    {
        try
        {
            computer_ = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
            };
            computer_.Open();
        }
        catch
        {
            // Sensor access unavailable (VM, restricted OS, no driver).
            // All properties will return null.
            computer_ = new Computer();
        }
    }

    /// <inheritdoc/>
    public void Update()
    {
        if (disposed_) return;

        float? cpu = null;
        float? gpu = null;
        string? gpuName = null;

        try
        {
            foreach (var hw in computer_.Hardware)
            {
                hw.Update();

                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        cpu = ReadCpuTemp(hw);
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        gpu     = ReadFirstTemp(hw);
                        gpuName = hw.Name;
                        break;
                }
            }
        }
        catch { /* sensor read failure — keep last known values */ }

        cpuTemperature_ = cpu ?? float.NaN;
        gpuTemperature_ = gpu ?? float.NaN;
        gpuName_        = gpuName;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!disposed_)
        {
            disposed_ = true;
            try { computer_.Close(); } catch { }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Prefers the "CPU Package" sensor; falls back to the first temperature
    /// sensor on the CPU hardware (Core #0, Core Average, etc.).
    /// </summary>
    private static float? ReadCpuTemp(IHardware cpu)
    {
        ISensor? packageSensor = null;
        ISensor? fallback      = null;

        foreach (var s in cpu.Sensors)
        {
            // Skip null or physically-impossible readings (0 = driver couldn't read MSR)
            if (s.SensorType != SensorType.Temperature) continue;
            if (s.Value is not float v || v <= 0f || v > 120f) continue;

            if (s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("Tdie",    StringComparison.OrdinalIgnoreCase)  ||
                s.Name.Contains("Tctl",    StringComparison.OrdinalIgnoreCase))
            {
                packageSensor = s;
            }

            fallback ??= s;
        }

        return (packageSensor ?? fallback)?.Value;
    }

    /// <summary>Returns the first valid temperature sensor value on <paramref name="hw"/>.</summary>
    private static float? ReadFirstTemp(IHardware hw)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == SensorType.Temperature &&
                s.Value is float v && v > 0f && v < 150f)
                return v;
        }
        return null;
    }
}
