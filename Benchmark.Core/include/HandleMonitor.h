/**
 * @file HandleMonitor.h
 * @brief Handle count and thread count monitor for a single process.
 *
 * Uses GetProcessHandleCount() for kernel handle count and
 * CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD) for thread enumeration.
 */

#pragma once

#include "IMonitor.h"
#include "HandleGuard.h"
#include <cstdint>
#include <mutex>

namespace Benchmark::Core
{

/**
 * @brief Tracks handle and thread counts for one process (ConcreteStrategy).
 *
 * @note Thread-safe: all mutable state is protected by mutex_.
 */
class HandleMonitor final : public IMonitor
{
public:
    bool update(HANDLE processHandle) noexcept override;
    void populate(ProcessMetricsSnapshot& snapshot) const noexcept override;
    void reset() noexcept override;

private:
    mutable std::mutex mutex_;

    uint32_t handleCount_ { 0 };
    uint32_t threadCount_ { 0 };
};

} // namespace Benchmark::Core
