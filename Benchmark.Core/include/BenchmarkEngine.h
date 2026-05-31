/**
 * @file BenchmarkEngine.h
 * @brief Facade that coordinates all metric monitors for a single process.
 *
 * BenchmarkEngine owns a vector of IMonitor instances (Strategy objects),
 * drives the background sampling loop, and exposes the latest snapshot
 * to callers through a thread-safe accessor. It is the Context in the
 * Strategy pattern and the Subject for change notification via callback.
 *
 * @par Design Patterns
 * - **Facade**: simplifies the multi-monitor API to start/stop/getSnapshot.
 * - **Strategy**: IMonitor implementations are swappable without touching the engine.
 * - **Observer** (lightweight): a registered MetricsCallback is invoked after
 *   each successful sampling cycle.
 */

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "ProcessMetrics.h"
#include "HandleGuard.h"
#include "IMonitor.h"

#include <atomic>
#include <chrono>
#include <cstdint>
#include <functional>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

namespace Benchmark::Core
{

/**
 * @brief Callback invoked on the sampling thread after each successful sample.
 *
 * @warning Keep implementations short; offload heavy work to a separate thread
 *          or queue to avoid blocking the sampling loop.
 */
using MetricsCallback = std::function<void(const ProcessMetricsSnapshot&)>;

/**
 * @brief Orchestrates all metric monitors for one process (Facade + Strategy Context).
 *
 * Lifecycle:
 * @code
 *   BenchmarkEngine engine(pid);
 *   engine.setCallback([](const ProcessMetricsSnapshot& s){ ... });
 *   engine.start();
 *   // ...
 *   engine.stop();
 * @endcode
 *
 * @note start()/stop()/setCallback() are thread-safe.
 *       Calling start() twice without an intervening stop() throws std::runtime_error.
 */
class BenchmarkEngine final
{
public:
    /**
     * @brief Constructs the engine and opens a handle to the target process.
     *
     * All monitors are registered here. Adding a new metric category only
     * requires instantiating the monitor and push_back()-ing it.
     *
     * @param processId  OS process identifier to monitor.
     * @param intervalMs Sampling interval in milliseconds (default: 1000).
     * @throws std::runtime_error if OpenProcess() fails for the given PID.
     */
    explicit BenchmarkEngine(uint32_t processId, uint32_t intervalMs = 1000);

    ~BenchmarkEngine();

    BenchmarkEngine(const BenchmarkEngine&)            = delete;
    BenchmarkEngine& operator=(const BenchmarkEngine&) = delete;

    /**
     * @brief Registers a callback to receive metric snapshots.
     *
     * Pass nullptr to remove the current callback. Safe to call at any time,
     * even while the engine is running.
     *
     * @param cb Callback function. Invoked on the background sampling thread.
     */
    void setCallback(MetricsCallback cb);

    /**
     * @brief Starts the background sampling loop.
     * @throws std::runtime_error if already running.
     */
    void start();

    /**
     * @brief Stops the background sampling loop and joins the thread.
     *
     * Idempotent — calling stop() when not running is a no-op.
     */
    void stop() noexcept;

    /** @brief Returns true while the sampling loop is active. */
    [[nodiscard]] bool isRunning() const noexcept;

    /**
     * @brief Returns a thread-safe copy of the most recently collected snapshot.
     * @return The latest snapshot, or a zero-initialised struct if no sample exists.
     */
    [[nodiscard]] ProcessMetricsSnapshot latestSnapshot() const noexcept;

    /** @brief Resets all accumulated statistics across every monitor. */
    void reset() noexcept;

private:
    void samplingLoop() noexcept;
    bool collectSample() noexcept;

    uint32_t   processId_;
    uint32_t   intervalMs_;
    HandleGuard processHandle_;

    std::vector<std::unique_ptr<IMonitor>> monitors_;

    mutable std::mutex callbackMutex_;
    MetricsCallback    callback_;

    mutable std::mutex     snapshotMutex_;
    ProcessMetricsSnapshot latestSnapshot_{};

    std::atomic<bool> running_{ false };
    std::thread       samplingThread_;
};

} // namespace Benchmark::Core
