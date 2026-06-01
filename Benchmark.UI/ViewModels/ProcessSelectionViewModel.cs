/**
 * @file ProcessSelectionViewModel.cs
 * @brief ViewModel for the Process Setup screen.
 *
 * Manages two orthogonal mode axes:
 *   BenchmarkMode  — Single (one process slot) vs Compare (two slots)
 *   ListingMode    — how processes are grouped in each picker:
 *                    Process | Group (same exe) | Tree (parent + descendants)
 */

using Benchmark.Interop;
using Benchmark.Models;
using Benchmark.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Benchmark.UI.ViewModels;

/// <summary>
/// ViewModel for the Process Setup step.
/// Exposes Single/Compare benchmark mode, Process/Group/Tree listing mode,
/// and filterable process lists for slots A and B.
/// </summary>
public sealed partial class ProcessSelectionViewModel : ViewModelBase
{
    private readonly IProcessDiscoveryService discoveryService_;
    private readonly IBenchmarkService        benchmarkService_;

    // Raw flat process list fetched from the OS
    private IReadOnlyList<ProcessInfo>            allProcesses_ = [];
    // PPID map cached on each Refresh — used for Tree mode
    private IReadOnlyDictionary<uint, uint>       parentMap_    = new Dictionary<uint, uint>();
    // Descendant counts per PID — computed together with parentMap_
    private IReadOnlyDictionary<uint, int>        descCounts_   = new Dictionary<uint, int>();

    // ── Filtered lists ────────────────────────────────────────────────────────

    /// <summary>Gets the filtered/grouped list for Process A picker.</summary>
    public ObservableCollection<ProcessInfo> FilteredProcessesA { get; } = [];

    /// <summary>Gets the filtered/grouped list for Process B picker.</summary>
    public ObservableCollection<ProcessInfo> FilteredProcessesB { get; } = [];

    // ── Selection ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBenchmarkCommand))]
    private ProcessInfo? selectedProcessA;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBenchmarkCommand))]
    private ProcessInfo? selectedProcessB;

    [ObservableProperty] private string searchTextA    = string.Empty;
    [ObservableProperty] private string searchTextB    = string.Empty;
    [ObservableProperty] private bool   isLoading;
    [ObservableProperty] private string statusMessage  = "Select a process to benchmark.";

    // ── Benchmark mode (Single / Compare) ────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleMode))]
    [NotifyPropertyChangedFor(nameof(IsCompareMode))]
    [NotifyPropertyChangedFor(nameof(ModeSubtitle))]
    [NotifyCanExecuteChangedFor(nameof(StartBenchmarkCommand))]
    private BenchmarkMode benchmarkMode = BenchmarkMode.Single;

    /// <summary>True when monitoring a single process slot.</summary>
    public bool IsSingleMode  => BenchmarkMode == BenchmarkMode.Single;

    /// <summary>True when two process slots are compared side-by-side.</summary>
    public bool IsCompareMode => BenchmarkMode == BenchmarkMode.Compare;

    /// <summary>Subtitle text that reflects the active benchmark mode.</summary>
    public string ModeSubtitle => IsSingleMode
        ? "Monitor one process or app in real-time."
        : "Select two processes or apps to compare side-by-side.";

    // ── Listing mode (Process / Group / Tree) ─────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListProcess))]
    [NotifyPropertyChangedFor(nameof(IsListGroup))]
    [NotifyPropertyChangedFor(nameof(IsListTree))]
    private ListingMode listingMode = ListingMode.Group;

    /// <summary>True when listing mode is Process (individual PIDs).</summary>
    public bool IsListProcess => ListingMode == ListingMode.Process;

    /// <summary>True when listing mode is Group (same-exe collapse).</summary>
    public bool IsListGroup   => ListingMode == ListingMode.Group;

    /// <summary>True when listing mode is Tree (parent + descendants).</summary>
    public bool IsListTree    => ListingMode == ListingMode.Tree;

    // ── Platform delegates ────────────────────────────────────────────────────

    /// <summary>Invoked on successful session start; set by MainWindowViewModel for navigation.</summary>
    public Action? OnBenchmarkStarted { get; set; }

    /// <summary>Platform file-picker delegate for slot A, injected by the View.</summary>
    public Func<Task>? BrowseForProcessAAsync { get; set; }

    /// <summary>Platform file-picker delegate for slot B, injected by the View.</summary>
    public Func<Task>? BrowseForProcessBAsync { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initialises with required services.</summary>
    public ProcessSelectionViewModel(
        IProcessDiscoveryService discoveryService,
        IBenchmarkService        benchmarkService)
    {
        discoveryService_ = discoveryService;
        benchmarkService_ = benchmarkService;
    }

    // ── Benchmark mode commands ───────────────────────────────────────────────

    [RelayCommand]
    private void SetSingleMode()
    {
        BenchmarkMode = BenchmarkMode.Single;
        StatusMessage = "Select a process to benchmark.";
    }

    [RelayCommand]
    private void SetCompareMode()
    {
        BenchmarkMode = BenchmarkMode.Compare;
        StatusMessage = "Select two processes to compare.";
    }

    // ── Listing mode commands ─────────────────────────────────────────────────

    [RelayCommand]
    private void SetListProcess()
    {
        ListingMode = ListingMode.Process;
        RefreshFilters();
    }

    [RelayCommand]
    private void SetListGroup()
    {
        ListingMode = ListingMode.Group;
        RefreshFilters();
    }

    [RelayCommand]
    private void SetListTree()
    {
        ListingMode = ListingMode.Tree;
        RefreshFilters();
    }

    // ── Process commands ──────────────────────────────────────────────────────

    /// <summary>Reloads the running process list from the OS and rebuilds the process tree.</summary>
    [RelayCommand]
    private async Task RefreshProcessesAsync()
    {
        IsLoading     = true;
        StatusMessage = "Refreshing process list…";
        try
        {
            // Process list via existing async API
            allProcesses_ = await discoveryService_.GetRunningProcessesAsync();

            // Tree snapshot is CPU-bound Win32 work — offload to thread pool
            (parentMap_, descCounts_) = await Task.Run(() =>
            {
                var pm = ProcessTreeHelper.GetParentPidMap();
                var dc = ProcessTreeHelper.GetDescendantCounts(pm);
                return (pm, dc);
            });

            RefreshFilters();
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

    [RelayCommand]
    private async Task BrowseExecutableAAsync()
    {
        if (BrowseForProcessAAsync is not null) await BrowseForProcessAAsync();
    }

    [RelayCommand]
    private async Task BrowseExecutableBAsync()
    {
        if (BrowseForProcessBAsync is not null) await BrowseForProcessBAsync();
    }

    /// <summary>Starts the benchmark session with the currently selected processes.</summary>
    [RelayCommand(CanExecute = nameof(CanStartBenchmark))]
    private async Task StartBenchmarkAsync()
    {
        IsLoading     = true;
        StatusMessage = "Starting benchmark session…";
        try
        {
            // In Tree mode, enrich the selected ProcessInfo with the live subtree PIDs
            var a = EnrichWithTree(SelectedProcessA!);
            var b = IsCompareMode ? EnrichWithTree(SelectedProcessB) : null;

            await benchmarkService_.StartAsync(a, b, BenchmarkMode);
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

    private bool CanStartBenchmark()
    {
        if (SelectedProcessA is null) return false;
        if (!IsCompareMode)           return true;
        return SelectedProcessB is not null &&
               SelectedProcessA.ProcessId != SelectedProcessB.ProcessId;
    }

    // ── Partial change handlers ───────────────────────────────────────────────

    partial void OnSearchTextAChanged(string value) => ApplyFilter(value, FilteredProcessesA);
    partial void OnSearchTextBChanged(string value) => ApplyFilter(value, FilteredProcessesB);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshFilters()
    {
        ApplyFilter(SearchTextA, FilteredProcessesA);
        ApplyFilter(SearchTextB, FilteredProcessesB);
    }

    private void ApplyFilter(string search, ObservableCollection<ProcessInfo> target)
    {
        // Exclude processes whose name could not be resolved (shown as "<pid>" or "<unknown>")
        IEnumerable<ProcessInfo> source = allProcesses_.Where(p => !p.Name.StartsWith('<'));

        if (!string.IsNullOrWhiteSpace(search))
            source = source.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.ProcessId.ToString().Contains(search));

        IEnumerable<ProcessInfo> result = ListingMode switch
        {
            ListingMode.Group => BuildGroupListing(source),
            ListingMode.Tree  => BuildTreeListing(source),
            _                 => source.OrderBy(p => p.Name),  // Process — flat list
        };

        target.Clear();
        foreach (var p in result)
            target.Add(p);
    }

    /// <summary>Group listing: one entry per unique executable name.</summary>
    private static IEnumerable<ProcessInfo> BuildGroupListing(IEnumerable<ProcessInfo> source)
        => source
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var sorted = g.OrderBy(p => p.ProcessId).ToList();
                var main   = sorted[0];
                int n      = sorted.Count;
                return main with
                {
                    InstanceCount = n,
                    AllProcessIds = n > 1 ? sorted.Select(p => p.ProcessId).ToList() : null,
                    GroupDescription = n > 1 ? $"{n} instances · PID {main.ProcessId}" : null,
                };
            })
            .OrderBy(p => p.Name);

    /// <summary>
    /// Tree listing: only root processes are shown (processes whose parent is
    /// not itself a known running process). Children are hidden — they will be
    /// included automatically via <see cref="EnrichWithTree"/> at start time.
    /// Each entry is annotated with its total descendant count.
    /// </summary>
    private IEnumerable<ProcessInfo> BuildTreeListing(IEnumerable<ProcessInfo> source)
    {
        // pid → name lookup for O(1) parent-name resolution
        var pidToName = allProcesses_.ToDictionary(p => p.ProcessId, p => p.Name);

        return source
            .Where(p =>
            {
                // Not a root only when parent is another process with the SAME exe name.
                // This keeps cross-exe children visible as roots (e.g. brave.exe spawned by
                // explorer.exe stays as a root), while same-name worker processes are hidden
                // (e.g. brave.exe renderer children of brave.exe are suppressed).
                if (!parentMap_.TryGetValue(p.ProcessId, out var ppid)) return true;
                if (!pidToName.TryGetValue(ppid, out var parentName))   return true;
                return !string.Equals(parentName, p.Name, StringComparison.OrdinalIgnoreCase);
            })
            .Select(p =>
            {
                int childCount = descCounts_.TryGetValue(p.ProcessId, out int n) ? n : 0;
                return p with
                {
                    InstanceCount    = 1 + childCount,
                    GroupDescription = childCount > 0
                        ? $"PID {p.ProcessId} · {childCount} child process{(childCount == 1 ? "" : "es")}"
                        : null,
                };
            })
            .OrderBy(p => p.Name);
    }

    /// <summary>
    /// In Tree mode, re-fetches the live process tree rooted at the selected PID
    /// and stores all descendant PIDs in <see cref="ProcessInfo.AllProcessIds"/>.
    /// In other modes, returns the process info unchanged.
    /// </summary>
    private ProcessInfo EnrichWithTree(ProcessInfo? info)
    {
        if (info is null) throw new ArgumentNullException(nameof(info));
        if (ListingMode != ListingMode.Tree) return info;

        // Re-snapshot the tree at start time so newly spawned children are included
        var liveParentMap = ProcessTreeHelper.GetParentPidMap();
        var treePids      = ProcessTreeHelper.GetTreePids(info.ProcessId, liveParentMap);

        int childCount = treePids.Count - 1;
        return info with
        {
            InstanceCount    = treePids.Count,
            AllProcessIds    = treePids.Count > 1 ? treePids : null,
            GroupDescription = childCount > 0
                ? $"PID {info.ProcessId} · {childCount} child process{(childCount == 1 ? "" : "es")}"
                : null,
        };
    }

    /// <summary>Called by the View after the user picks an executable via file dialog.</summary>
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
