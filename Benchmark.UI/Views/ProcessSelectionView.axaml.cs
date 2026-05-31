/**
 * @file ProcessSelectionView.axaml.cs
 * @brief Code-behind for ProcessSelectionView.
 *
 * Contains only the platform file-open dialog, which cannot be expressed in AXAML.
 * The ViewModel exposes BrowseExecutableAAsync / BrowseExecutableBAsync as
 * partial commands; the view injects the picked path via BrowseExecutableWithPathAsync().
 *
 * Pattern: the ViewModel signals "I need a path" via an Action<bool> delegate,
 * and the View provides the platform implementation of that delegate.
 */

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Benchmark.UI.ViewModels;

namespace Benchmark.UI.Views;

/// <summary>
/// Code-behind for the process selection screen.
/// Minimal — provides only the file-open dialog bridge.
/// </summary>
public partial class ProcessSelectionView : UserControl
{
    /// <summary>Initializes view components.</summary>
    public ProcessSelectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ProcessSelectionViewModel vm) return;

        // Inject platform dialog providers into the ViewModel.
        // The ViewModel calls these when "Browse…" is clicked.
        vm.BrowseForProcessAAsync = () => OpenFilePickerAsync(vm, forA: true);
        vm.BrowseForProcessBAsync = () => OpenFilePickerAsync(vm, forA: false);
    }

    private async Task OpenFilePickerAsync(ProcessSelectionViewModel vm, bool forA)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title         = $"Select Executable for Process {(forA ? 'A' : 'B')}",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Executable") { Patterns = ["*.exe"] },
                    new FilePickerFileType("All files")  { Patterns = ["*.*"] },
                ],
            });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            await vm.BrowseExecutableWithPathAsync(path, forA);
    }
}
