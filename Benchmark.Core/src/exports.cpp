/**
 * @file exports.cpp
 * @brief Implementation of the C-compatible DLL export functions.
 *
 * Each export function is a thin adapter between the C API surface and the
 * C++ BenchmarkEngine. Exceptions thrown by C++ code are caught here and
 * translated to error codes — exception propagation across a DLL boundary
 * is undefined behavior and must be prevented.
 */

// BENCHMARKCORE_EXPORTS is injected by CMake via target_compile_definitions — no redefinition here.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <psapi.h>
#include <tlhelp32.h>

#pragma comment(lib, "psapi.lib")

#include "BenchmarkCore.h"
#include "BenchmarkEngine.h"
#include "ProcessAccess.h"

#include <cstring>
#include <vector>

using Engine = Benchmark::Core::BenchmarkEngine;

// ── Session lifecycle ─────────────────────────────────────────────────────────

BENCHMARK_API BenchmarkHandle BenchmarkCreate(uint32_t processId, uint32_t intervalMs)
{
    try
    {
        return static_cast<BenchmarkHandle>(new Engine(processId, intervalMs));
    }
    catch (...)
    {
        return nullptr;
    }
}

BENCHMARK_API int32_t BenchmarkStart(BenchmarkHandle handle)
{
    if (!handle) return -1;
    try
    {
        static_cast<Engine*>(handle)->start();
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

BENCHMARK_API void BenchmarkStop(BenchmarkHandle handle)
{
    if (handle)
        static_cast<Engine*>(handle)->stop();
}

BENCHMARK_API void BenchmarkDestroy(BenchmarkHandle handle)
{
    if (handle)
        delete static_cast<Engine*>(handle);
}

// ── Data retrieval ────────────────────────────────────────────────────────────

BENCHMARK_API int32_t BenchmarkGetSnapshot(
    BenchmarkHandle         handle,
    ProcessMetricsSnapshot* outSnapshot)
{
    if (!handle || !outSnapshot) return -1;
    *outSnapshot = static_cast<Engine*>(handle)->latestSnapshot();
    return 0;
}

// ── Process enumeration ───────────────────────────────────────────────────────

BENCHMARK_API int32_t BenchmarkEnumerateProcesses(
    uint32_t* outPids,
    uint32_t  maxCount,
    uint32_t* outCount)
{
    if (!outPids || !outCount || maxCount == 0) return -1;

    std::vector<DWORD> pids(maxCount);
    DWORD bytesNeeded = 0;

    if (!EnumProcesses(pids.data(), maxCount * sizeof(DWORD), &bytesNeeded))
        return -1;

    const uint32_t count = std::min(
        static_cast<uint32_t>(bytesNeeded / sizeof(DWORD)),
        maxCount);

    for (uint32_t i = 0; i < count; ++i)
        outPids[i] = static_cast<uint32_t>(pids[i]);

    *outCount = count;
    return 0;
}

BENCHMARK_API int32_t BenchmarkGetProcessName(
    uint32_t pid,
    char*    outName,
    uint32_t bufferLen)
{
    return Benchmark::Core::queryProcessBaseName(pid, outName, bufferLen);
}

// ── Utility ───────────────────────────────────────────────────────────────────

BENCHMARK_API void BenchmarkReset(BenchmarkHandle handle)
{
    if (handle)
        static_cast<Engine*>(handle)->reset();
}
