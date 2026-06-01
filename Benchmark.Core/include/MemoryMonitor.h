/**
 * @file MemoryMonitor.h
 * @brief Memory usage monitor for a single process.
 *
 * Retrieves working set, private bytes, and peak working set via
 * GetProcessMemoryInfo() (PSAPI), which requires PROCESS_QUERY_INFORMATION
 * | PROCESS_VM_READ access rights on the process handle.
 */

#pragma once

#include "IMonitor.h"
#include <cstdint>
#include <mutex>

namespace Benchmark::Core
{

/**
 * @brief Tracks working set and private bytes for one process (ConcreteStrategy).
 *
 * @note Thread-safe: all mutable state is protected by mutex_.
 */
class MemoryMonitor final : public IMonitor
{
public:
    bool update(HANDLE processHandle) noexcept override;
    void populate(ProcessMetricsSnapshot& snapshot) const noexcept override;
    void reset() noexcept override;

private:
    mutable std::mutex mutex_;

    uint64_t workingSetBytes_        { 0 };
    uint64_t privateBytes_           { 0 };
    uint64_t peakWorkingSetBytes_    { 0 };
    uint64_t privateWorkingSetBytes_ { 0 };
};

} // namespace Benchmark::Core
