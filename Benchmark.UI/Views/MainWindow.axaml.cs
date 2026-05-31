/**
 * @file MainWindow.axaml.cs
 * @brief Code-behind for the main application window.
 *
 * Minimal code-behind — only platform bootstrap that cannot be expressed in AXAML.
 * All logic lives in MainWindowViewModel.
 */

using Avalonia.Controls;

namespace Benchmark.UI.Views;

/// <summary>
/// Main application window shell.
/// Contains only Avalonia boilerplate; behaviour is driven by MainWindowViewModel.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Initializes window components.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
