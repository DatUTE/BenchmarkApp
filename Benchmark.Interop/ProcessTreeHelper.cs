/**
 * @file ProcessTreeHelper.cs
 * @brief Win32-based process tree builder using CreateToolhelp32Snapshot.
 *
 * Retrieves parent-process relationships for all running processes via
 * the Toolhelp32 snapshot API (the same API Windows Task Manager uses
 * internally to build its "Process Tree" view).
 */

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Benchmark.Interop;

/// <summary>
/// Builds and queries the running process tree using
/// <c>CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS)</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProcessTreeHelper
{
    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint   dwSize;
        public uint   cntUsage;
        public uint   th32ProcessID;
        public nint   th32DefaultHeapID;   // ULONG_PTR — 8 bytes on x64
        public uint   th32ModuleID;
        public uint   cntThreads;
        public uint   th32ParentProcessID;
        public int    pcPriClassBase;
        public uint   dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32FirstW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32NextW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint hObject);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a map of { pid → parentPid } for every process currently running.
    /// Uses a single Toolhelp32 snapshot — call once per refresh cycle.
    /// </summary>
    public static IReadOnlyDictionary<uint, uint> GetParentPidMap()
    {
        var map      = new Dictionary<uint, uint>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot == new nint(-1))   // INVALID_HANDLE_VALUE
            return map;

        try
        {
            var entry = new PROCESSENTRY32W
            {
                dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>()
            };

            if (!Process32FirstW(snapshot, ref entry))
                return map;

            do { map[entry.th32ProcessID] = entry.th32ParentProcessID; }
            while (Process32NextW(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return map;
    }

    /// <summary>
    /// Returns all PIDs that belong to the process tree rooted at
    /// <paramref name="rootPid"/> (root included). Uses BFS so depth of the
    /// tree does not matter.
    /// </summary>
    /// <param name="rootPid">The top-level (parent) process.</param>
    /// <param name="parentMap">Snapshot from <see cref="GetParentPidMap"/>.</param>
    public static IReadOnlyList<uint> GetTreePids(
        uint rootPid,
        IReadOnlyDictionary<uint, uint> parentMap)
    {
        // Build children map: parentPid → [childPid, ...]
        var children = new Dictionary<uint, List<uint>>();
        foreach (var (pid, ppid) in parentMap)
        {
            if (!children.TryGetValue(ppid, out var list))
                children[ppid] = list = [];
            list.Add(pid);
        }

        // BFS from root
        var visited = new HashSet<uint>();
        var result  = new List<uint>();
        var queue   = new Queue<uint>();

        queue.Enqueue(rootPid);
        visited.Add(rootPid);

        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            result.Add(pid);

            if (!children.TryGetValue(pid, out var kids)) continue;
            foreach (var kid in kids)
            {
                if (visited.Add(kid))   // Add returns false if already present
                    queue.Enqueue(kid);
            }
        }

        return result;
    }

    /// <summary>
    /// Counts immediate + transitive children for each PID.
    /// Returns { pid → descendantCount } for PIDs that have at least one descendant.
    /// </summary>
    public static IReadOnlyDictionary<uint, int> GetDescendantCounts(
        IReadOnlyDictionary<uint, uint> parentMap)
    {
        var childrenOf = new Dictionary<uint, List<uint>>();
        foreach (var (pid, ppid) in parentMap)
        {
            if (!childrenOf.TryGetValue(ppid, out var list))
                childrenOf[ppid] = list = [];
            list.Add(pid);
        }

        var counts = new Dictionary<uint, int>();

        // DFS count for each process that has children
        foreach (var root in childrenOf.Keys)
            counts[root] = CountDescendants(root, childrenOf, new HashSet<uint>());

        return counts;
    }

    private static int CountDescendants(
        uint pid,
        Dictionary<uint, List<uint>> childrenOf,
        HashSet<uint> visited)
    {
        if (!visited.Add(pid)) return 0;
        if (!childrenOf.TryGetValue(pid, out var kids)) return 0;

        int count = kids.Count;
        foreach (var kid in kids)
            count += CountDescendants(kid, childrenOf, visited);
        return count;
    }
}
