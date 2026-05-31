/**
 * @file CpuMonitor.cpp
 * @brief Implementation of CpuMonitor.
 *
 * Computes CPU usage by comparing successive GetProcessTimes() readings
 * against GetSystemTimes() deltas, giving a percentage normalized to
 * total system time so that heavy single-threaded processes don't exceed 100%.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "CpuMonitor.h"
#include <algorithm>

namespace Benchmark::Core
{

CpuMonitor::CpuMonitor()
{
    SYSTEM_INFO si{};
    GetSystemInfo(&si);
    numProcessors_ = static_cast<int>(si.dwNumberOfProcessors);
    if (numProcessors_ < 1)
        numProcessors_ = 1;
}

bool CpuMonitor::update(HANDLE processHandle) noexcept
{
    FILETIME createFt{}, exitFt{}, kernelFt{}, userFt{};
    if (!GetProcessTimes(processHandle, &createFt, &exitFt, &kernelFt, &userFt))
        return false;

    FILETIME sysIdleFt{}, sysKernelFt{}, sysUserFt{};
    if (!GetSystemTimes(&sysIdleFt, &sysKernelFt, &sysUserFt))
        return false;

    const uint64_t curKernel = fileTimeToU64(kernelFt);
    const uint64_t curUser   = fileTimeToU64(userFt);
    // System time is kernel + user across all processors
    const uint64_t curSystem = fileTimeToU64(sysKernelFt) + fileTimeToU64(sysUserFt);

    std::lock_guard lock(mutex_);

    if (!initialized_)
    {
        prevKernelTime_ = curKernel;
        prevUserTime_   = curUser;
        prevSystemTime_ = curSystem;
        initialized_    = true;
        return true;  // First sample: store reference, emit no data
    }

    const uint64_t deltaProcess = (curKernel - prevKernelTime_) + (curUser - prevUserTime_);
    const uint64_t deltaSystem  = curSystem - prevSystemTime_;

    prevKernelTime_ = curKernel;
    prevUserTime_   = curUser;
    prevSystemTime_ = curSystem;

    currentCpu_ = (deltaSystem > 0)
        ? std::clamp(
              (static_cast<double>(deltaProcess) / static_cast<double>(deltaSystem)) * 100.0,
              0.0, 100.0)
        : 0.0;

    peakCpu_ = std::max(peakCpu_, currentCpu_);

    // Welford's online mean — numerically stable, O(1) space
    ++sampleCount_;
    averageCpu_ += (currentCpu_ - averageCpu_) / static_cast<double>(sampleCount_);

    return true;
}

void CpuMonitor::populate(ProcessMetricsSnapshot& snapshot) const noexcept
{
    std::lock_guard lock(mutex_);
    snapshot.cpuUsagePercent   = currentCpu_;
    snapshot.averageCpuPercent = averageCpu_;
    snapshot.peakCpuPercent    = peakCpu_;
}

void CpuMonitor::reset() noexcept
{
    std::lock_guard lock(mutex_);
    currentCpu_     = 0.0;
    averageCpu_     = 0.0;
    peakCpu_        = 0.0;
    sampleCount_    = 0;
    initialized_    = false;
    prevKernelTime_ = 0;
    prevUserTime_   = 0;
    prevSystemTime_ = 0;
}

uint64_t CpuMonitor::fileTimeToU64(const FILETIME& ft) noexcept
{
    return (static_cast<uint64_t>(ft.dwHighDateTime) << 32)
         |  static_cast<uint64_t>(ft.dwLowDateTime);
}

} // namespace Benchmark::Core
