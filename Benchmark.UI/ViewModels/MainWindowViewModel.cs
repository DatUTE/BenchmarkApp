/**
 * @file MainWindowViewModel.cs
 * @brief Top-level ViewModel for the main application shell.
 *
 * Manages navigation between the three top-level views using a simple
 * CurrentView property (no complex navigation framework needed for MVP).
 * Injects child ViewModels via DI and wires up cross-ViewModel callbacks.
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Benchmark.UI.ViewModels;

/// <summary>
/// Identifies which top-level page is currently visible.
/// </summary>
public enum AppPage
{
    ProcessSelection,
    Dashboard,
    Export,
}

/// <summary>
/// Shell ViewModel — manages navigation and hosts child ViewModels.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    // ── Child ViewModels (injected via DI) ────────────────────────────────────

    /// <summary>Gets the ProcessSelection page ViewModel.</summary>
    public ProcessSelectionViewModel ProcessSelection { get; }

    /// <summary>Gets the Dashboard page ViewModel.</summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>Gets the Export page ViewModel.</summary>
    public ExportViewModel Export { get; }

    // ── Navigation state ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnProcessSelection))]
    [NotifyPropertyChangedFor(nameof(IsOnDashboard))]
    [NotifyPropertyChangedFor(nameof(IsOnExport))]
    private AppPage currentPage = AppPage.ProcessSelection;

    /// <summary>Gets whether the ProcessSelection page is active.</summary>
    public bool IsOnProcessSelection => CurrentPage == AppPage.ProcessSelection;

    /// <summary>Gets whether the Dashboard page is active.</summary>
    public bool IsOnDashboard => CurrentPage == AppPage.Dashboard;

    /// <summary>Gets whether the Export page is active.</summary>
    public bool IsOnExport => CurrentPage == AppPage.Export;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Composes all child ViewModels and sets up cross-VM wiring.</summary>
    public MainWindowViewModel(
        ProcessSelectionViewModel processSelection,
        DashboardViewModel        dashboard,
        ExportViewModel           export)
    {
        ProcessSelection = processSelection;
        Dashboard        = dashboard;
        Export           = export;

        // When the user starts a benchmark, navigate to the Dashboard automatically
        ProcessSelection.OnBenchmarkStarted = () =>
        {
            Dashboard.ProcessNameA = processSelection.SelectedProcessA?.Name ?? "Process A";
            Dashboard.ProcessNameB = processSelection.SelectedProcessB?.Name ?? "Process B";
            NavigateTo(AppPage.Dashboard);
        };
    }

    // ── Navigation commands ───────────────────────────────────────────────────

    /// <summary>Navigates to the ProcessSelection page.</summary>
    [RelayCommand]
    private void GoToProcessSelection() => NavigateTo(AppPage.ProcessSelection);

    /// <summary>Navigates to the Dashboard page.</summary>
    [RelayCommand]
    private void GoToDashboard() => NavigateTo(AppPage.Dashboard);

    /// <summary>Navigates to the Export page.</summary>
    [RelayCommand]
    private void GoToExport() => NavigateTo(AppPage.Export);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void NavigateTo(AppPage page) => CurrentPage = page;
}
