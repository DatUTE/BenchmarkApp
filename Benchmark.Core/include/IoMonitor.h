/**
 * @file IoMonitor.h
 * @brief Disk I/O monitor for a single process.
 *
 * Retrieves cumulative read and write byte counts via GetProcessIoCounters(),
 * which requires PROCESS_QUERY_INFORMATION access rights.
 */

#pragma once

#include "IMonitor.h"
#include <cstdint>
#include <mutex>

namespace Benchmark::Core
{

/**
 * @brief Tracks cumulative disk I/O byte totals for one process (ConcreteStrategy).
 *
 * Values are cumulative from process start, not per-interval deltas.
 *
 * @note Thread-safe: all mutable state is protected by mutex_.
 */
class IoMonitor final : public IMonitor
{
public:
    bool update(HANDLE processHandle) noexcept override;
    void populate(ProcessMetricsSnapshot& snapshot) const noexcept override;
    void reset() noexcept override;

private:
    mutable std::mutex mutex_;

    uint64_t readBytes_  { 0 };
    uint64_t writeBytes_ { 0 };
};

} // namespace Benchmark::Core
