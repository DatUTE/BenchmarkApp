/**
 * @file ProcessMetrics.h
 * @brief Data structures representing process performance metrics.
 *
 * These POD structures are designed for P/Invoke interoperability with C#.
 * Layout is fixed with pack(8) to match the C# [StructLayout(Sequential, Pack=8)]
 * declaration in Benchmark.Interop.
 */

#pragma once

#include <cstdint>

#pragma pack(push, 8)

/**
 * @brief A complete snapshot of all monitored metrics at a single point in time.
 *
 * Every field uses a fixed-width type so the struct layout is identical
 * on the C++ and C# sides of the P/Invoke boundary.
 */
struct ProcessMetricsSnapshot
{
    /** @brief OS process identifier. */
    uint32_t processId;

    // ── CPU ─────────────────────────────────────────────────────────────────
    /** @brief Instantaneous CPU usage [0.0, 100.0]. */
    double cpuUsagePercent;

    /** @brief Running mean CPU usage since monitoring started (Welford online mean). */
    double averageCpuPercent;

    /** @brief Maximum CPU usage observed since monitoring started. */
    double peakCpuPercent;

    // ── Memory ───────────────────────────────────────────────────────────────
    /** @brief Current working set (physical RAM consumed) in bytes. */
    uint64_t workingSetBytes;

    /** @brief Private committed bytes (virtual memory exclusively owned by this process). */
    uint64_t privateBytes;

    /** @brief Peak working set in bytes (since process start, from OS). */
    uint64_t peakWorkingSetBytes;

    // ── Threads & Handles ───────────────────────────────────────────────────
    /** @brief Current number of threads. */
    uint32_t threadCount;

    /** @brief Current number of open handles. */
    uint32_t handleCount;

    // ── Disk I/O (cumulative totals) ─────────────────────────────────────────
    /** @brief Total bytes read from storage since process start. */
    uint64_t ioReadBytes;

    /** @brief Total bytes written to storage since process start. */
    uint64_t ioWriteBytes;

    // ── Process Lifetime ─────────────────────────────────────────────────────
    /** @brief Process creation time as a FILETIME uint64 (100-ns intervals since 1601-01-01 UTC). */
    uint64_t startTimeUtc;

    /** @brief Seconds elapsed since process creation. */
    uint64_t uptimeSeconds;

    // ── Timing ────────────────────────────────────────────────────────────────
    /** @brief QueryPerformanceCounter tick when this sample was taken. */
    uint64_t sampleTimestamp;
};

#pragma pack(pop)