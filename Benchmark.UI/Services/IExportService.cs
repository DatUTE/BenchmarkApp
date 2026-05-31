/**
 * @file IExportService.cs
 * @brief Service interface for exporting benchmark session data.
 */

using Benchmark.Models;

namespace Benchmark.UI.Services;

/// <summary>Supported output formats for benchmark data export.</summary>
public enum ExportFormat
{
    /// <summary>Comma-separated values, one row per snapshot.</summary>
    Csv,

    /// <summary>JSON array of snapshot objects.</summary>
    Json,
}

/// <summary>
/// Writes a completed or in-progress <see cref="BenchmarkSession"/> to disk.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Serializes the session data and writes it to the specified file path.
    /// </summary>
    /// <param name="session">The session to export.</param>
    /// <param name="outputPath">Absolute path of the output file.</param>
    /// <param name="format">The output format to use.</param>
    /// <exception cref="IOException">Thrown if the file cannot be written.</exception>
    Task ExportSessionAsync(
        BenchmarkSession session,
        string           outputPath,
        ExportFormat     format);
}
