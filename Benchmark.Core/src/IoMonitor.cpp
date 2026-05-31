/**
 * @file IoMonitor.cpp
 * @brief Implementation of IoMonitor.
 *
 * GetProcessIoCounters() returns cumulative byte counts since process creation.
 * The UI layer is responsible for computing per-interval deltas if desired.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "IoMonitor.h"

namespace Benchmark::Core
{

bool IoMonitor::update(HANDLE processHandle) noexcept
{
    IO_COUNTERS counters{};
    if (!GetProcessIoCounters(processHandle, &counters))
        return false;

    std::lock_guard lock(mutex_);
    readBytes_  = counters.ReadTransferCount;
    writeBytes_ = counters.WriteTransferCount;
    return true;
}

void IoMonitor::populate(ProcessMetricsSnapshot& snapshot) const noexcept
{
    std::lock_guard lock(mutex_);
    snapshot.ioReadBytes  = readBytes_;
    snapshot.ioWriteBytes = writeBytes_;
}

void IoMonitor::reset() noexcept
{
    std::lock_guard lock(mutex_);
    readBytes_  = 0;
    writeBytes_ = 0;
}

} // namespace Benchmark::Core
