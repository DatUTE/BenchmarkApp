/**
 * @file ExportService.cs
 * @brief Concrete implementation of IExportService.
 *
 * Supports CSV and JSON export. Both formats include a header/metadata block
 * followed by all snapshots for Process A and Process B with timestamps.
 */

using Benchmark.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.UI.Services;

/// <summary>
/// Exports benchmark session data to CSV or JSON files.
/// </summary>
public sealed class ExportService : IExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented            = true,
        DefaultIgnoreCondition   = JsonIgnoreCondition.Never,
        PropertyNamingPolicy     = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc/>
    public async Task ExportSessionAsync(
        BenchmarkSession session,
        string           outputPath,
        ExportFormat     format)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await using var stream = new FileStream(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 65536, useAsync: true);

        switch (format)
        {
            case ExportFormat.Csv:
                await WriteCsvAsync(session, stream);
                break;
            case ExportFormat.Json:
                await WriteJsonAsync(session, stream);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    // ── CSV ───────────────────────────────────────────────────────────────────

    private static async Task WriteCsvAsync(BenchmarkSession session, Stream stream)
    {
        await using var writer = new StreamWriter(stream, Encoding.UTF8);

        // Metadata
        await writer.WriteLineAsync($"# BenchmarkTool Export");
        await writer.WriteLineAsync($"# Session ID: {session.Id}");
        await writer.WriteLineAsync($"# Started:    {session.StartedAt:O}");
        await writer.WriteLineAsync($"# Duration:   {session.Duration}");
        await writer.WriteLineAsync($"# Process A:  {session.ProcessA}");
        await writer.WriteLineAsync($"# Process B:  {session.ProcessB}");
        await writer.WriteLineAsync();

        // Header row
        const string Header =
            "Timestamp,Process,ProcessId,CpuPercent,AvgCpuPercent,PeakCpuPercent," +
            "WorkingSetMB,PrivateMB,PeakWorkingSetMB," +
            "Threads,Handles," +
            "IoReadMB,IoWriteMB," +
            "UptimeSeconds";

        await writer.WriteLineAsync(Header);

        // Data rows — interleave A and B sorted by timestamp
        var allRows = session.SnapshotsA.Select(s => (s, "A"))
            .Concat(session.SnapshotsB.Select(s => (s, "B")))
            .OrderBy(t => t.s.Timestamp);

        foreach (var (snap, label) in allRows)
            await writer.WriteLineAsync(FormatCsvRow(snap, label));
    }

    private static string FormatCsvRow(MetricSnapshot s, string label)
    {
        static double Mb(long bytes) => bytes / (1024.0 * 1024.0);
        var c = CultureInfo.InvariantCulture;

        return string.Join(",",
            s.Timestamp.ToString("O"),
            label,
            s.ProcessId.ToString(c),
            s.CpuPercent.ToString("F2", c),
            s.AverageCpuPercent.ToString("F2", c),
            s.PeakCpuPercent.ToString("F2", c),
            Mb(s.WorkingSetBytes).ToString("F1", c),
            Mb(s.PrivateBytes).ToString("F1", c),
            Mb(s.PeakWorkingSetBytes).ToString("F1", c),
            s.ThreadCount.ToString(c),
            s.HandleCount.ToString(c),
            Mb(s.IoReadBytes).ToString("F2", c),
            Mb(s.IoWriteBytes).ToString("F2", c),
            ((long)s.Uptime.TotalSeconds).ToString(c));
    }

    // ── JSON ──────────────────────────────────────────────────────────────────

    private static async Task WriteJsonAsync(BenchmarkSession session, Stream stream)
    {
        var payload = new
        {
            sessionId  = session.Id,
            startedAt  = session.StartedAt,
            endedAt    = session.EndedAt,
            duration   = session.Duration.TotalSeconds,
            processA   = new { session.ProcessA.ProcessId, session.ProcessA.Name },
            processB   = new { session.ProcessB.ProcessId, session.ProcessB.Name },
            snapshotsA = session.SnapshotsA.Select(MapSnapshot),
            snapshotsB = session.SnapshotsB.Select(MapSnapshot),
        };

        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions);
    }

    private static object MapSnapshot(MetricSnapshot s) => new
    {
        timestamp          = s.Timestamp,
        processId          = s.ProcessId,
        cpuPercent         = Math.Round(s.CpuPercent,        2),
        avgCpuPercent      = Math.Round(s.AverageCpuPercent, 2),
        peakCpuPercent     = Math.Round(s.PeakCpuPercent,    2),
        workingSetMB       = Math.Round(s.WorkingSetBytes     / (1024.0 * 1024), 1),
        privateMB          = Math.Round(s.PrivateBytes        / (1024.0 * 1024), 1),
        peakWorkingSetMB   = Math.Round(s.PeakWorkingSetBytes / (1024.0 * 1024), 1),
        threads            = s.ThreadCount,
        handles            = s.HandleCount,
        ioReadMB           = Math.Round(s.IoReadBytes  / (1024.0 * 1024), 2),
        ioWriteMB          = Math.Round(s.IoWriteBytes / (1024.0 * 1024), 2),
        uptimeSeconds      = (long)s.Uptime.TotalSeconds,
    };
}
