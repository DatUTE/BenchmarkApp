/**
 * @file DashboardView.axaml.cs
 * @brief Code-behind for DashboardView.
 *
 * Intentionally minimal — all logic lives in DashboardViewModel.
 */

using Avalonia.Controls;

namespace Benchmark.UI.Views;

/// <summary>
/// Real-time monitoring dashboard view.
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>Initializes view components.</summary>
    public DashboardView()
    {
        InitializeComponent();
    }
}
