/**
 * @file BenchmarkEngine.cpp
 * @brief Implementation of BenchmarkEngine.
 *
 * The engine opens a process handle with the minimal required access rights,
 * registers all monitors, then drives the sampling loop on a dedicated thread.
 * Adding a new metric category is a single push_back() in the constructor.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "BenchmarkEngine.h"
#include "CpuMonitor.h"
#include "MemoryMonitor.h"
#include "IoMonitor.h"
#include "HandleMonitor.h"
#include "LifetimeMonitor.h"
#include "ProcessAccess.h"

#include <stdexcept>
#include <string>

namespace Benchmark::Core
{

BenchmarkEngine::BenchmarkEngine(uint32_t processId, uint32_t intervalMs)
    : processId_{ processId }
    , intervalMs_{ intervalMs }
{
    HANDLE h = openProcessForMonitoring(processId);
    if (!h)
    {
        throw std::runtime_error(
            "BenchmarkEngine: cannot open process " + std::to_string(processId) +
            " (error " + std::to_string(GetLastError()) + ")");
    }

    processHandle_ = HandleGuard{ h };

    // Register monitors — order determines populate() call order on snapshot assembly.
    monitors_.push_back(std::make_unique<CpuMonitor>());
    monitors_.push_back(std::make_unique<MemoryMonitor>());
    monitors_.push_back(std::make_unique<IoMonitor>());
    monitors_.push_back(std::make_unique<HandleMonitor>());
    monitors_.push_back(std::make_unique<LifetimeMonitor>());
}

BenchmarkEngine::~BenchmarkEngine()
{
    stop();
}

void BenchmarkEngine::setCallback(MetricsCallback cb)
{
    std::lock_guard lock(callbackMutex_);
    callback_ = std::move(cb);
}

void BenchmarkEngine::start()
{
    if (running_.exchange(true))
        throw std::runtime_error("BenchmarkEngine: already running");

    samplingThread_ = std::thread(&BenchmarkEngine::samplingLoop, this);
}

void BenchmarkEngine::stop() noexcept
{
    running_.store(false);
    if (samplingThread_.joinable())
        samplingThread_.join();
}

bool BenchmarkEngine::isRunning() const noexcept
{
    return running_.load(std::memory_order_relaxed);
}

ProcessMetricsSnapshot BenchmarkEngine::latestSnapshot() const noexcept
{
    std::lock_guard lock(snapshotMutex_);
    return latestSnapshot_;
}

void BenchmarkEngine::reset() noexcept
{
    for (auto& monitor : monitors_)
        monitor->reset();
}

void BenchmarkEngine::samplingLoop() noexcept
{
    while (running_.load(std::memory_order_relaxed))
    {
        collectSample();
        std::this_thread::sleep_for(std::chrono::milliseconds(intervalMs_));
    }
}

bool BenchmarkEngine::collectSample() noexcept
{
    ProcessMetricsSnapshot snap{};
    snap.processId = processId_;

    // Record QPC timestamp for the snapshot
    LARGE_INTEGER qpc{};
    QueryPerformanceCounter(&qpc);
    snap.sampleTimestamp = static_cast<uint64_t>(qpc.QuadPart);

    bool anySuccess = false;
    for (auto& monitor : monitors_)
    {
        if (monitor->update(processHandle_.get()))
            anySuccess = true;
        monitor->populate(snap);
    }

    if (anySuccess)
    {
        {
            std::lock_guard snapLock(snapshotMutex_);
            latestSnapshot_ = snap;
        }
        std::lock_guard cbLock(callbackMutex_);
        if (callback_)
            callback_(snap);
    }

    return anySuccess;
}

} // namespace Benchmark::Core
