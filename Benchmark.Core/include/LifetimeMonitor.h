/**
 * @file LifetimeMonitor.h
 * @brief Process lifetime monitor: start time and uptime.
 *
 * Reads the process creation time from GetProcessTimes() and computes
 * elapsed time relative to the current system time.
 */

#pragma once

#include "IMonitor.h"
#include <cstdint>
#include <mutex>

namespace Benchmark::Core
{

/**
 * @brief Tracks process start time and computes uptime (ConcreteStrategy).
 *
 * The creation time is read once (on first successful update) and cached;
 * uptime is recomputed on every subsequent sample.
 *
 * @note Thread-safe: all mutable state is protected by mutex_.
 */
class LifetimeMonitor final : public IMonitor
{
public:
    bool update(HANDLE processHandle) noexcept override;
    void populate(ProcessMetricsSnapshot& snapshot) const noexcept override;
    void reset() noexcept override;

private:
    mutable std::mutex mutex_;

    /** @brief Process creation time as FILETIME uint64, set on first update. */
    uint64_t startTimeUtc_  { 0 };
    uint64_t uptimeSeconds_ { 0 };
    bool     initialized_   { false };
};

} // namespace Benchmark::Core
