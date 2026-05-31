/**
 * @file ExportView.axaml.cs
 * @brief Code-behind for ExportView.
 *
 * Wires the platform file-save dialog to ExportViewModel.BrowseOutputPathCommand.
 */

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Benchmark.UI.ViewModels;

namespace Benchmark.UI.Views;

/// <summary>
/// Export results view — platform dialog bridge only.
/// </summary>
public partial class ExportView : UserControl
{
    /// <summary>Initializes view components and wires the platform dialog.</summary>
    public ExportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ExportViewModel vm)
        {
            vm.RequestFilePath = async () =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is null) return;

                var isCsv = vm.ExportAsCsv;
                var file  = await topLevel.StorageProvider.SaveFilePickerAsync(
                    new FilePickerSaveOptions
                    {
                        Title           = "Save Benchmark Export",
                        SuggestedFileName = $"benchmark_{DateTime.Now:yyyyMMdd_HHmmss}",
                        FileTypeChoices =
                        [
                            isCsv
                                ? new FilePickerFileType("CSV") { Patterns = ["*.csv"] }
                                : new FilePickerFileType("JSON") { Patterns = ["*.json"] },
                        ],
                    });

                vm.PickedPath = file?.TryGetLocalPath() ?? string.Empty;
            };
        }
    }
}
