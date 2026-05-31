/**
 * @file ProcessSelectionViewModel.cs
 * @brief ViewModel for the Process Selection screen.
 *
 * Presents a filterable list of running processes for Application A
 * and Application B. Supports both "pick from running list" and
 * "browse for executable" selection modes.
 */

using Benchmark.Models;
using Benchmark.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Benchmark.UI.ViewModels;

/// <summary>
/// ViewModel for the process selection step.
/// Exposes two independent process selectors (A and B) with filtering.
/// </summary>
public sealed partial class ProcessSelectionViewModel : ViewModelBase
{
    private readonly IProcessDiscoveryService discoveryService_;
    private readonly IBenchmarkService        benchmarkService_;

    // ── Filtered lists ────────────────────────────────────────────────────────
    private IReadOnlyList<ProcessInfo> allProcesses_ = [];

    /// <summary>Gets the current filtered list of processes for Process A picker.</summary>
    public ObservableCollection<ProcessInfo> FilteredProcessesA { get; } = [];

    /// <summary>Gets the current filtered list of processes for Process B picker.</summary>
    public ObservableCollection<ProcessInfo> FilteredProcessesB { get; } = [];

    // ── Observable properties ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBenchmarkCommand))]
    private ProcessInfo? selectedProcessA;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBenchmarkCommand))]
    private ProcessInfo? selectedProcessB;

    [ObservableProperty] private string searchTextA = string.Empty;
    [ObservableProperty] private string searchTextB = string.Empty;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Select two processes to compare.";

    // ── Platform delegates (injected by the View) ─────────────────────────────

    /// <summary>
    /// Invoked when the user successfully starts a benchmark session.
    /// Set by MainWindowViewModel to trigger navigation.
    /// </summary>
    public Action? OnBenchmarkStarted { get; set; }

    /// <summary>
    /// Platform file-picker delegate for Process A, injected by ProcessSelectionView.
    /// The View sets this to show the OS file-open dialog.
    /// </summary>
    public Func<Task>? BrowseForProcessAAsync { get; set; }

    /// <summary>
    /// Platform file-picker delegate for Process B, injected by ProcessSelectionView.
    /// </summary>
    public Func<Task>? BrowseForProcessBAsync { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initializes the ViewModel with required services.</summary>
    public ProcessSelectionViewModel(
        IProcessDiscoveryService discoveryService,
        IBenchmarkService        benchmarkService)
    {
        discoveryService_ = discoveryService;
        benchmarkService_ = benchmarkService;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Reloads the running process list from the OS.</summary>
    [RelayCommand]
    private async Task RefreshProcessesAsync()
    {
        IsLoading     = true;
        StatusMessage = "Refreshing process list…";
        try
        {
            allProcesses_ = await discoveryService_.GetRunningProcessesAsync();
            ApplyFilter(SearchTextA, FilteredProcessesA);
            ApplyFilter(SearchTextB, FilteredProcessesB);
            StatusMessage = $"{allProcesses_.Count} processes found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Opens a file browser and launches or attaches to the chosen executable (Process A).</summary>
    [RelayCommand]
    private async Task BrowseExecutableAAsync()
    {
        if (BrowseForProcessAAsync is not null)
            await BrowseForProcessAAsync();
    }

    /// <summary>Opens a file browser and launches or attaches to the chosen executable (Process B).</summary>
    [RelayCommand]
    private async Task BrowseExecutableBAsync()
    {
        if (BrowseForProcessBAsync is not null)
            await BrowseForProcessBAsync();
    }

    /// <summary>Starts the benchmark session with the two selected processes.</summary>
    [RelayCommand(CanExecute = nameof(CanStartBenchmark))]
    private async Task StartBenchmarkAsync()
    {
        IsLoading     = true;
        StatusMessage = "Starting benchmark session…";
        try
        {
            var processA = discoveryService_.ResolveForMonitoring(SelectedProcessA!);
            var processB = discoveryService_.ResolveForMonitoring(SelectedProcessB!);

            if (processA.ProcessId != SelectedProcessA!.ProcessId)
                SelectedProcessA = processA;
            if (processB.ProcessId != SelectedProcessB!.ProcessId)
                SelectedProcessB = processB;

            await benchmarkService_.StartAsync(processA, processB);
            OnBenchmarkStarted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanStartBenchmark() =>
        SelectedProcessA is not null &&
        SelectedProcessB is not null &&
        SelectedProcessA.ProcessId != SelectedProcessB.ProcessId;

    // ── Partial property change handlers (generated by CommunityToolkit) ──────

    partial void OnSearchTextAChanged(string value)
        => ApplyFilter(value, FilteredProcessesA);

    partial void OnSearchTextBChanged(string value)
        => ApplyFilter(value, FilteredProcessesB);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyFilter(string search, ObservableCollection<ProcessInfo> target)
    {
        var filtered = string.IsNullOrWhiteSpace(search)
            ? allProcesses_
            : allProcesses_.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.ProcessId.ToString().Contains(search));

        target.Clear();
        foreach (var p in filtered)
            target.Add(p);
    }

    /// <summary>
    /// Called by the View after the user picks an executable path.
    /// </summary>
    public async Task<ProcessInfo?> BrowseExecutableWithPathAsync(string path, bool forA)
    {
        try
        {
            var info = await discoveryService_.GetProcessFromExecutableAsync(path);
            if (info is not null)
            {
                if (forA) SelectedProcessA = info;
                else      SelectedProcessB = info;
                StatusMessage = $"Process {(forA ? 'A' : 'B')} set to {info}.";
            }
            return info;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error launching process: {ex.Message}";
            return null;
        }
    }
}
