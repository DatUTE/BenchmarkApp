/**
 * @file DashboardViewModel.cs
 * @brief ViewModel for the real-time monitoring dashboard.
 *
 * Maintains rolling time-series data for LiveCharts2 charts and exposes
 * formatted summary strings for the comparison card grid.
 * Updates arrive on a background thread and are dispatched to the UI thread
 * via Avalonia's Dispatcher before mutating observable properties.
 */

using Avalonia.Threading;
using Benchmark.Models;
using Benchmark.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace Benchmark.UI.ViewModels;

/// <summary>
/// Drives the real-time comparison dashboard, including LiveCharts2 series
/// for CPU, memory, and thread history charts.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private const int RollingWindowSize = 60;  // Keep 60 seconds of history

    private readonly IBenchmarkService benchmarkService_;

    // ── Rolling chart data ────────────────────────────────────────────────────
    private readonly ObservableCollection<DateTimePoint> cpuHistoryA_    = [];
    private readonly ObservableCollection<DateTimePoint> cpuHistoryB_    = [];
    private readonly ObservableCollection<DateTimePoint> memHistoryA_    = [];
    private readonly ObservableCollection<DateTimePoint> memHistoryB_    = [];
    private readonly ObservableCollection<DateTimePoint> threadHistoryA_ = [];
    private readonly ObservableCollection<DateTimePoint> threadHistoryB_ = [];

    // ── Summary card properties ───────────────────────────────────────────────
    [ObservableProperty] private string processNameA  = "Process A";
    [ObservableProperty] private string processNameB  = "Process B";
    [ObservableProperty] private string cpuA          = "—";
    [ObservableProperty] private string cpuB          = "—";
    [ObservableProperty] private string avgCpuA       = "—";
    [ObservableProperty] private string avgCpuB       = "—";
    [ObservableProperty] private string peakCpuA      = "—";
    [ObservableProperty] private string peakCpuB      = "—";
    [ObservableProperty] private string ramA          = "—";
    [ObservableProperty] private string ramB          = "—";
    [ObservableProperty] private string privateA      = "—";
    [ObservableProperty] private string privateB      = "—";
    [ObservableProperty] private string threadsA      = "—";
    [ObservableProperty] private string threadsB      = "—";
    [ObservableProperty] private string handlesA      = "—";
    [ObservableProperty] private string handlesB      = "—";
    [ObservableProperty] private string ioReadA       = "—";
    [ObservableProperty] private string ioReadB       = "—";
    [ObservableProperty] private string ioWriteA      = "—";
    [ObservableProperty] private string ioWriteB      = "—";
    [ObservableProperty] private string uptimeA       = "—";
    [ObservableProperty] private string uptimeB       = "—";
    [ObservableProperty] private string statusMessage = "Waiting for benchmark session…";
    [ObservableProperty] private bool   isRunning;

    // ── Chart series ─────────────────────────────────────────────────────────

    /// <summary>Gets the CPU usage time-series for the chart.</summary>
    public ISeries[] CpuSeries { get; }

    /// <summary>Gets the memory (working set MB) time-series for the chart.</summary>
    public ISeries[] MemSeries { get; }

    /// <summary>Gets the thread count time-series for the chart.</summary>
    public ISeries[] ThreadSeries { get; }

    /// <summary>Gets X-axis configuration shared by all charts (datetime axis).</summary>
    public Axis[] DateTimeAxes { get; } =
    [
        new DateTimeAxis(TimeSpan.FromSeconds(1), dt => dt.ToString("HH:mm:ss"))
        {
            Name          = "Time",
            NamePaint     = new SolidColorPaint(SKColors.Gray),
            LabelsPaint   = new SolidColorPaint(SKColors.Gray),
            SeparatorsPaint = new SolidColorPaint(SKColors.DimGray) { StrokeThickness = 0.5f },
        }
    ];

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initializes the dashboard and subscribes to benchmark updates.</summary>
    public DashboardViewModel(IBenchmarkService benchmarkService)
    {
        benchmarkService_ = benchmarkService;
        benchmarkService_.SnapshotsUpdated += OnSnapshotsUpdated;

        // Build chart series with colour-coded lines for A (blue) and B (orange)
        CpuSeries =
        [
            MakeLineSeries("CPU A", cpuHistoryA_, SKColor.Parse("#4FC3F7")),
            MakeLineSeries("CPU B", cpuHistoryB_, SKColor.Parse("#FFB74D")),
        ];

        MemSeries =
        [
            MakeLineSeries("RAM A (MB)", memHistoryA_,    SKColor.Parse("#4FC3F7")),
            MakeLineSeries("RAM B (MB)", memHistoryB_,    SKColor.Parse("#FFB74D")),
        ];

        ThreadSeries =
        [
            MakeLineSeries("Threads A", threadHistoryA_, SKColor.Parse("#4FC3F7")),
            MakeLineSeries("Threads B", threadHistoryB_, SKColor.Parse("#FFB74D")),
        ];

        // Populate initial state from session (if one already started)
        UpdateFromSession();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Stops the running benchmark session.</summary>
    [RelayCommand]
    private async Task StopBenchmarkAsync()
    {
        await benchmarkService_.StopAsync();
        IsRunning     = false;
        StatusMessage = "Session stopped.";
    }

    /// <summary>Resets accumulated statistics (averages, peaks).</summary>
    [RelayCommand]
    private void ClearHistory()
    {
        cpuHistoryA_.Clear();    cpuHistoryB_.Clear();
        memHistoryA_.Clear();    memHistoryB_.Clear();
        threadHistoryA_.Clear(); threadHistoryB_.Clear();
    }

    // ── Event handling ────────────────────────────────────────────────────────

    private void OnSnapshotsUpdated(object? sender, SnapshotPair pair)
    {
        // Dispatch UI updates to the Avalonia UI thread
        Dispatcher.UIThread.Post(() => ApplySnapshots(pair.A, pair.B));
    }

    private void ApplySnapshots(MetricSnapshot a, MetricSnapshot b)
    {
        IsRunning     = true;
        StatusMessage = $"Running · Last sample: {DateTime.Now:HH:mm:ss}";

        var now = DateTime.Now;

        // ── CPU
        AppendRolling(cpuHistoryA_, new DateTimePoint(now, a.CpuPercent));
        AppendRolling(cpuHistoryB_, new DateTimePoint(now, b.CpuPercent));
        CpuA     = $"{a.CpuPercent:F1}%";
        CpuB     = $"{b.CpuPercent:F1}%";
        AvgCpuA  = $"{a.AverageCpuPercent:F1}%";
        AvgCpuB  = $"{b.AverageCpuPercent:F1}%";
        PeakCpuA = $"{a.PeakCpuPercent:F1}%";
        PeakCpuB = $"{b.PeakCpuPercent:F1}%";

        // ── Memory
        double memAmb = a.WorkingSetBytes / (1024.0 * 1024);
        double memBmb = b.WorkingSetBytes / (1024.0 * 1024);
        AppendRolling(memHistoryA_, new DateTimePoint(now, memAmb));
        AppendRolling(memHistoryB_, new DateTimePoint(now, memBmb));
        RamA     = a.WorkingSetFormatted;
        RamB     = b.WorkingSetFormatted;
        PrivateA = a.PrivateBytesFormatted;
        PrivateB = b.PrivateBytesFormatted;

        // ── Threads
        AppendRolling(threadHistoryA_, new DateTimePoint(now, a.ThreadCount));
        AppendRolling(threadHistoryB_, new DateTimePoint(now, b.ThreadCount));
        ThreadsA = a.ThreadCount.ToString();
        ThreadsB = b.ThreadCount.ToString();
        HandlesA = a.HandleCount.ToString();
        HandlesB = b.HandleCount.ToString();

        // ── I/O
        IoReadA  = FormatBytes(a.IoReadBytes);
        IoReadB  = FormatBytes(b.IoReadBytes);
        IoWriteA = FormatBytes(a.IoWriteBytes);
        IoWriteB = FormatBytes(b.IoWriteBytes);

        // ── Uptime
        UptimeA = FormatUptime(a.Uptime);
        UptimeB = FormatUptime(b.Uptime);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateFromSession()
    {
        var session = benchmarkService_.CurrentSession;
        if (session is null) return;

        ProcessNameA = session.ProcessA.Name;
        ProcessNameB = session.ProcessB.Name;
    }

    private static void AppendRolling(
        ObservableCollection<DateTimePoint> series,
        DateTimePoint point)
    {
        series.Add(point);
        while (series.Count > RollingWindowSize)
            series.RemoveAt(0);
    }

    private static LineSeries<DateTimePoint> MakeLineSeries(
        string name,
        ObservableCollection<DateTimePoint> values,
        SKColor color)
    {
        var paint = new SolidColorPaint(color) { StrokeThickness = 2 };
        return new LineSeries<DateTimePoint>
        {
            Name             = name,
            Values           = values,
            Stroke           = paint,
            Fill             = new SolidColorPaint(color.WithAlpha(30)),
            GeometrySize     = 0,
            LineSmoothness   = 0.3,
        };
    }

    private static string FormatBytes(long bytes)
    {
        const long MB = 1024L * 1024;
        const long GB = MB * 1024;
        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F2} GB",
            >= MB => $"{bytes / (double)MB:F1} MB",
            _     => $"{bytes / 1024.0:F1} KB",
        };
    }

    private static string FormatUptime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}h {t.Minutes:D2}m {t.Seconds:D2}s"
            : $"{t.Minutes}m {t.Seconds:D2}s";

    /// <inheritdoc/>
    public void Dispose()
    {
        benchmarkService_.SnapshotsUpdated -= OnSnapshotsUpdated;
    }
}
