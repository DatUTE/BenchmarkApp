/**
 * @file MemoryMonitor.cpp
 * @brief Implementation of MemoryMonitor.
 *
 * Memory metrics are gathered from two sources:
 *
 * 1. GetProcessMemoryInfo() — documented PSAPI, requires
 *    PROCESS_QUERY_INFORMATION | PROCESS_VM_READ (or LIMITED variant).
 *    Provides: WorkingSetSize, PeakWorkingSetSize, PrivateUsage.
 *
 * 2. NtQuerySystemInformation(SystemProcessInformation) — semi-documented ntdll.
 *    Returns per-process WorkingSetPrivateSize for ALL running processes in one
 *    system call, without needing any per-process access rights beyond what we
 *    already hold. This matches the value in the Windows Task Manager "Memory"
 *    column and bypasses sandbox restrictions that block QueryWorkingSet() or
 *    NtQueryInformationProcess(ProcessVmCounters) on protected processes.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <psapi.h>
#include <vector>
#include <cstdint>

#pragma comment(lib, "psapi.lib")

#include "MemoryMonitor.h"

namespace Benchmark::Core
{

// ── NT type shims ─────────────────────────────────────────────────────────────

using NtStatus = LONG;

constexpr NtStatus STATUS_NT_SUCCESS               = 0;
constexpr NtStatus STATUS_INFO_LENGTH_MISMATCH     = static_cast<NtStatus>(0xC0000004);

using NtQuerySystemInformationFn = NtStatus(WINAPI*)(
    ULONG  SystemInformationClass,
    PVOID  SystemInformation,
    ULONG  SystemInformationLength,
    PULONG ReturnLength);

// ── SYSTEM_PROCESS_INFORMATION field offsets (x64, Windows Vista+) ───────────
//
// Verified against Geoff Chappell's documentation and Windows SDK symbols.
//
//   +0x000  ULONG  NextEntryOffset
//   +0x004  ULONG  NumberOfThreads
//   +0x008  LARGE_INTEGER  WorkingSetPrivateSize    ← what we need (bytes)
//   ...
//   +0x050  HANDLE UniqueProcessId                  (8 bytes on x64)
//
constexpr size_t kOffsetNextEntry       = 0x000;   // uint32_t
constexpr size_t kOffsetWorkingSetPriv  = 0x008;   // int64_t  (LARGE_INTEGER)
constexpr size_t kOffsetUniqueProcessId = 0x050;   // uint64_t (HANDLE on x64)

// ── System-wide private working set query ─────────────────────────────────────

static NtQuerySystemInformationFn resolveNtQuerySystemInfo() noexcept
{
    return reinterpret_cast<NtQuerySystemInformationFn>(
        GetProcAddress(GetModuleHandleW(L"ntdll.dll"),
                       "NtQuerySystemInformation"));
}

/**
 * @brief Returns the Private Working Set (bytes) for @p targetPid via a
 *        system-wide snapshot query.
 *
 * Uses NtQuerySystemInformation(SystemProcessInformation = 5).  Unlike
 * NtQueryInformationProcess or QueryWorkingSet, this does NOT require the
 * process handle to carry PROCESS_VM_READ, making it work even for sandboxed
 * or restricted processes (e.g., Brave/Chrome renderer workers).
 *
 * Time complexity: O(total number of running processes) per call.
 * For a 1 Hz sampling rate this is negligible.
 */
static uint64_t queryPrivateWorkingSetBytes(uint32_t targetPid) noexcept
{
    static const NtQuerySystemInformationFn NtQuerySysInfo = resolveNtQuerySystemInfo();
    if (!NtQuerySysInfo) return 0;

    constexpr ULONG SystemProcessInformation = 5;

    // Grow buffer until NtQuerySystemInformation returns STATUS_SUCCESS.
    // Typical size is 100–300 KB for ~400 processes; start at 256 KB.
    std::vector<uint8_t> buf(256 * 1024);

    for (;;)
    {
        ULONG needed = 0;
        const NtStatus st = NtQuerySysInfo(
            SystemProcessInformation,
            buf.data(),
            static_cast<ULONG>(buf.size()),
            &needed);

        if (st == STATUS_INFO_LENGTH_MISMATCH)
        {
            buf.resize(static_cast<size_t>(needed) + 0x8000);
            continue;
        }
        if (st != STATUS_NT_SUCCESS)
            return 0;

        // Walk the linked list of SYSTEM_PROCESS_INFORMATION entries
        const uint8_t* p = buf.data();
        for (;;)
        {
            const uint32_t nextOffset = *reinterpret_cast<const uint32_t*>(p + kOffsetNextEntry);
            const uint64_t pid        = *reinterpret_cast<const uint64_t*>(p + kOffsetUniqueProcessId);

            if (pid == targetPid)
            {
                // WorkingSetPrivateSize is LARGE_INTEGER; read as int64, clamp to 0
                const int64_t ws = *reinterpret_cast<const int64_t*>(p + kOffsetWorkingSetPriv);
                return ws > 0 ? static_cast<uint64_t>(ws) : 0;
            }

            if (nextOffset == 0) break;  // last entry
            p += nextOffset;
        }

        return 0;  // PID not found in snapshot
    }
}

// ── IMonitor interface ────────────────────────────────────────────────────────

bool MemoryMonitor::update(HANDLE processHandle) noexcept
{
    PROCESS_MEMORY_COUNTERS_EX pmc{};
    pmc.cb = sizeof(pmc);

    if (!GetProcessMemoryInfo(
            processHandle,
            reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&pmc),
            sizeof(pmc)))
    {
        return false;
    }

    // Resolve PID from handle without requiring a stored field in this class
    const uint32_t pid       = GetProcessId(processHandle);
    const uint64_t privateWS = pid ? queryPrivateWorkingSetBytes(pid) : 0;

    std::lock_guard lock(mutex_);
    workingSetBytes_        = pmc.WorkingSetSize;
    privateBytes_           = pmc.PrivateUsage;
    peakWorkingSetBytes_    = pmc.PeakWorkingSetSize;
    privateWorkingSetBytes_ = privateWS;
    return true;
}

void MemoryMonitor::populate(ProcessMetricsSnapshot& snapshot) const noexcept
{
    std::lock_guard lock(mutex_);
    snapshot.workingSetBytes        = workingSetBytes_;
    snapshot.privateBytes           = privateBytes_;
    snapshot.peakWorkingSetBytes    = peakWorkingSetBytes_;
    snapshot.privateWorkingSetBytes = privateWorkingSetBytes_;
}

void MemoryMonitor::reset() noexcept
{
    std::lock_guard lock(mutex_);
    workingSetBytes_        = 0;
    privateBytes_           = 0;
    peakWorkingSetBytes_    = 0;
    privateWorkingSetBytes_ = 0;
}

} // namespace Benchmark::Core
