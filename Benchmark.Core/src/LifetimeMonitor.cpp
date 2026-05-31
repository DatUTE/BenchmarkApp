/**
 * @file LifetimeMonitor.cpp
 * @brief Implementation of LifetimeMonitor.
 *
 * GetProcessTimes() provides the creation time as a FILETIME.
 * GetSystemTimeAsFileTime() gives the current time.
 * Uptime = (now - creation) / 10,000,000 (converting 100-ns ticks to seconds).
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "LifetimeMonitor.h"

namespace Benchmark::Core
{

bool LifetimeMonitor::update(HANDLE processHandle) noexcept
{
    FILETIME createFt{}, exitFt{}, kernelFt{}, userFt{};
    if (!GetProcessTimes(processHandle, &createFt, &exitFt, &kernelFt, &userFt))
        return false;

    const uint64_t startFt =
        (static_cast<uint64_t>(createFt.dwHighDateTime) << 32) | createFt.dwLowDateTime;

    FILETIME nowFt{};
    GetSystemTimeAsFileTime(&nowFt);
    const uint64_t nowU64 =
        (static_cast<uint64_t>(nowFt.dwHighDateTime) << 32) | nowFt.dwLowDateTime;

    // 100-ns intervals → seconds
    const uint64_t elapsedHns = (nowU64 > startFt) ? (nowU64 - startFt) : 0ULL;
    const uint64_t uptimeSec  = elapsedHns / 10'000'000ULL;

    std::lock_guard lock(mutex_);
    startTimeUtc_  = startFt;
    uptimeSeconds_ = uptimeSec;
    initialized_   = true;
    return true;
}

void LifetimeMonitor::populate(ProcessMetricsSnapshot& snapshot) const noexcept
{
    std::lock_guard lock(mutex_);
    snapshot.startTimeUtc  = startTimeUtc_;
    snapshot.uptimeSeconds = uptimeSeconds_;
}

void LifetimeMonitor::reset() noexcept
{
    std::lock_guard lock(mutex_);
    startTimeUtc_  = 0;
    uptimeSeconds_ = 0;
    initialized_   = false;
}

} // namespace Benchmark::Core
