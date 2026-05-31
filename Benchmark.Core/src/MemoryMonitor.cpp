/**
 * @file MemoryMonitor.cpp
 * @brief Implementation of MemoryMonitor.
 *
 * Uses GetProcessMemoryInfo() from PSAPI to retrieve both the extended
 * (EX) struct that contains PrivateUsage (private bytes), as well as
 * the standard WorkingSetSize and PeakWorkingSetSize fields.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <psapi.h>

#pragma comment(lib, "psapi.lib")

#include "MemoryMonitor.h"

namespace Benchmark::Core
{

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

    std::lock_guard lock(mutex_);
    workingSetBytes_     = pmc.WorkingSetSize;
    privateBytes_        = pmc.PrivateUsage;
    peakWorkingSetBytes_ = pmc.PeakWorkingSetSize;
    return true;
}

void MemoryMonitor::populate(ProcessMetricsSnapshot& snapshot) const noexcept
{
    std::lock_guard lock(mutex_);
    snapshot.workingSetBytes     = workingSetBytes_;
    snapshot.privateBytes        = privateBytes_;
    snapshot.peakWorkingSetBytes = peakWorkingSetBytes_;
}

void MemoryMonitor::reset() noexcept
{
    std::lock_guard lock(mutex_);
    workingSetBytes_     = 0;
    privateBytes_        = 0;
    peakWorkingSetBytes_ = 0;
}

} // namespace Benchmark::Core
