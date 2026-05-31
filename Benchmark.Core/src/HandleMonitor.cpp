/**
 * @file HandleMonitor.cpp
 * @brief Implementation of HandleMonitor.
 *
 * Handle count: GetProcessHandleCount() — requires PROCESS_QUERY_INFORMATION.
 * Thread count: CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD) filtered by PID.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <tlhelp32.h>

#include "HandleMonitor.h"

namespace Benchmark::Core
{

bool HandleMonitor::update(HANDLE processHandle) noexcept
{
    DWORD handleCount = 0;
    if (!GetProcessHandleCount(processHandle, &handleCount))
        return false;

    const DWORD pid = GetProcessId(processHandle);
    if (pid == 0)
        return false;

    // Enumerate threads using a Toolhelp snapshot — RAII'd via HandleGuard
    HandleGuard snap{ CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0) };
    if (!snap.isValid())
        return false;

    uint32_t threadCount = 0;
    THREADENTRY32 te{ .dwSize = sizeof(THREADENTRY32) };

    if (Thread32First(snap.get(), &te))
    {
        do
        {
            if (te.th32OwnerProcessID == pid)
                ++threadCount;
        }
        while (Thread32Next(snap.get(), &te));
    }

    std::lock_guard lock(mutex_);
    handleCount_ = static_cast<uint32_t>(handleCount);
    threadCount_ = threadCount;
    return true;
}

void HandleMonitor::populate(ProcessMetricsSnapshot& snapshot) const noexcept
{
    std::lock_guard lock(mutex_);
    snapshot.handleCount = handleCount_;
    snapshot.threadCount = threadCount_;
}

void HandleMonitor::reset() noexcept
{
    std::lock_guard lock(mutex_);
    handleCount_ = 0;
    threadCount_ = 0;
}

} // namespace Benchmark::Core
