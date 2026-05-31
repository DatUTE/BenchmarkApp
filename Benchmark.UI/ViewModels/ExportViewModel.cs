/**
 * @file ExportViewModel.cs
 * @brief ViewModel for the export panel.
 *
 * Lets the user choose output format (CSV/JSON) and output path,
 * then delegates to IExportService.
 */

using Benchmark.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Benchmark.UI.ViewModels;

/// <summary>
/// Drives the Export panel — format selection, path picking, and export execution.
/// </summary>
public sealed partial class ExportViewModel : ViewModelBase
{
    private readonly IBenchmarkService benchmarkService_;
    private readonly IExportService    exportService_;

    [ObservableProperty] private string  outputPath    = string.Empty;
    [ObservableProperty] private bool    exportAsCsv   = true;
    [ObservableProperty] private bool    exportAsJson;
    [ObservableProperty] private bool    isExporting;
    [ObservableProperty] private string  statusMessage = "Choose a format and output path, then click Export.";
    [ObservableProperty] private bool    exportSuccess;

    /// <summary>Invoked by the View to supply a file path from the platform dialog.</summary>
    public Action? RequestFilePath { get; set; }

    /// <summary>Set by the View after the user picks a path from the platform dialog.</summary>
    public string? PickedPath { get; set; }

    /// <summary>Initializes the ViewModel with required services.</summary>
    public ExportViewModel(IBenchmarkService benchmarkService, IExportService exportService)
    {
        benchmarkService_ = benchmarkService;
        exportService_    = exportService;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Triggers the platform file-save dialog (via the View's action).</summary>
    [RelayCommand]
    private void BrowseOutputPath()
    {
        RequestFilePath?.Invoke();
        if (!string.IsNullOrWhiteSpace(PickedPath))
            OutputPath = PickedPath;
    }

    /// <summary>Exports the current session to the selected path and format.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var session = benchmarkService_.CurrentSession;
        if (session is null)
        {
            StatusMessage = "No active session to export.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "Please select an output path first.";
            return;
        }

        IsExporting   = true;
        ExportSuccess = false;
        StatusMessage = "Exporting…";

        try
        {
            var format = ExportAsCsv ? ExportFormat.Csv : ExportFormat.Json;
            await exportService_.ExportSessionAsync(session, OutputPath, format);
            StatusMessage = $"Exported successfully to {Path.GetFileName(OutputPath)}.";
            ExportSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            ExportSuccess = false;
        }
        finally
        {
            IsExporting = false;
        }
    }

    private bool CanExport() =>
        !IsExporting &&
        benchmarkService_.CurrentSession is not null &&
        !string.IsNullOrWhiteSpace(OutputPath);

    // ── Partial handlers ──────────────────────────────────────────────────────

    partial void OnIsExportingChanged(bool value) => ExportCommand.NotifyCanExecuteChanged();
    partial void OnOutputPathChanged(string value) => ExportCommand.NotifyCanExecuteChanged();
}
