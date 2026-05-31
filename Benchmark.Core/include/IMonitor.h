/**
 * @file IMonitor.h
 * @brief Abstract interface for process metric monitors (Strategy pattern).
 *
 * BenchmarkEngine holds a polymorphic collection of IMonitor instances and
 * drives the sampling cycle. Each concrete monitor encapsulates exactly one
 * metric category (CPU, memory, I/O, …), keeping the engine free of
 * category-specific logic.
 *
 * @par Strategy Pattern
 * IMonitor is the Strategy interface. Concrete monitors (CpuMonitor,
 * MemoryMonitor, …) are ConcreteStrategies. BenchmarkEngine is the Context.
 */

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include "ProcessMetrics.h"

namespace Benchmark::Core
{

/**
 * @brief Abstract base for all process metric monitors.
 *
 * Lifecycle per sampling cycle:
 *   1. BenchmarkEngine calls update() on a background thread.
 *   2. If update() returns true, BenchmarkEngine calls populate() to
 *      transfer the latest values into the shared snapshot.
 *   3. On session reset, BenchmarkEngine calls reset() to clear
 *      accumulated state (averages, peaks, etc.).
 *
 * @note All implementations must be thread-safe; update() and populate()
 *       may be called from different threads.
 */
class IMonitor
{
public:
    virtual ~IMonitor() = default;

    /**
     * @brief Samples the metric(s) owned by this monitor.
     *
     * Called once per sampling interval on the background sampling thread.
     * Implementations must hold an internal mutex when updating shared state.
     *
     * @param processHandle An open HANDLE to the monitored process.
     *        The required access rights depend on the specific monitor
     *        (e.g., PROCESS_QUERY_INFORMATION, PROCESS_VM_READ).
     * @return true  if the sample was collected successfully.
     * @return false if the operation failed (e.g., process no longer exists).
     */
    virtual bool update(HANDLE processHandle) noexcept = 0;

    /**
     * @brief Populates the relevant fields of a metrics snapshot.
     *
     * Called after update() on the same thread. Must be non-blocking and
     * fast — the snapshot is assembled while holding no external lock.
     *
     * @param[out] snapshot The snapshot struct to fill in.
     */
    virtual void populate(ProcessMetricsSnapshot& snapshot) const noexcept = 0;

    /**
     * @brief Resets all accumulated statistics to their initial state.
     *
     * Called when the user requests a session reset. After reset(),
     * the monitor behaves as if no samples have been taken.
     */
    virtual void reset() noexcept = 0;
};

} // namespace Benchmark::Core