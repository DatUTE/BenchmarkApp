/**
 * @file CpuMonitor.h
 * @brief CPU usage monitor for a single process.
 *
 * Computes instantaneous, average, and peak CPU utilization by comparing
 * successive GetProcessTimes() readings and normalizing to logical processor
 * count so that 100% means "one full core".
 */

#pragma once

#include "IMonitor.h"
#include <cstdint>
#include <mutex>

namespace Benchmark::Core
{

/**
 * @brief Tracks CPU utilization for one process (ConcreteStrategy).
 *
 * Algorithm:
 *   - cpuDelta  = (kernelTimeDelta + userTimeDelta) per sample interval
 *   - sysDelta  = (kernelSystemDelta + userSystemDelta) per sample interval
 *   - usage%    = (cpuDelta / sysDelta) * 100
 *
 * The first call to update() is used solely for initialization and produces
 * no output (delta can't be computed without a prior reference point).
 *
 * @note Thread-safe: all mutable state is protected by mutex_.
 */
class CpuMonitor final : public IMonitor
{
public:
    /** @brief Queries the logical processor count for normalization. */
    CpuMonitor();

    bool update(HANDLE processHandle) noexcept override;
    void populate(ProcessMetricsSnapshot& snapshot) const noexcept override;
    void reset() noexcept override;

private:
    /**
     * @brief Converts a FILETIME to a uint64 for arithmetic.
     * @param ft The FILETIME to convert.
     * @return 100-nanosecond interval count.
     */
    static uint64_t fileTimeToU64(const FILETIME& ft) noexcept;

    mutable std::mutex mutex_;

    // ── Reference values from the previous sample ────────────────────────
    uint64_t prevKernelTime_  { 0 };
    uint64_t prevUserTime_    { 0 };
    uint64_t prevSystemTime_  { 0 };
    bool     initialized_     { false };

    // ── Accumulated statistics ────────────────────────────────────────────
    double   currentCpu_   { 0.0 };
    double   averageCpu_   { 0.0 };  ///< Welford online mean
    double   peakCpu_      { 0.0 };
    uint64_t sampleCount_  { 0 };

    int numProcessors_ { 1 };  ///< Logical processor count for display scaling
};

} // namespace Benchmark::Core