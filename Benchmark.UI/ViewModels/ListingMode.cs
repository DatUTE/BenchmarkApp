namespace Benchmark.UI.ViewModels;

/// <summary>
/// Controls how processes are grouped and displayed in the process picker.
/// Orthogonal to <see cref="Services.BenchmarkMode"/> (Single vs Compare).
/// </summary>
public enum ListingMode
{
    /// <summary>
    /// Show each OS process as a separate entry.
    /// Monitors exactly one PID — identical to classic Process Explorer behaviour.
    /// </summary>
    Process,

    /// <summary>
    /// Collapse all processes that share the same executable name into one entry.
    /// Aggregates CPU, memory, and I/O across all instances.
    /// Useful for multi-process apps like browsers that spawn many same-named workers.
    /// </summary>
    Group,

    /// <summary>
    /// Show each process individually but, when one is selected, automatically
    /// include its entire subtree (parent + all transitive children).
    /// Mirrors the "Process Tree" view in Windows Task Manager.
    /// Useful for apps that spawn differently-named child processes
    /// (e.g., VS Code → node.exe workers, Python scripts, etc.).
    /// </summary>
    Tree,
}
