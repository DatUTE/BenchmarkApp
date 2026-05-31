/**
 * @file BenchmarkCore.h
 * @brief C-compatible exported API for P/Invoke interoperability.
 *
 * Exposes the BenchmarkEngine functionality as a flat C API so that C#
 * can consume it via P/Invoke without COM or C++/CLI.
 *
 * Calling convention: cdecl (default for C functions on MSVC x64).
 * All exports use plain C types or pointers to avoid ABI mismatches.
 *
 * @note When including this header in C++ translation units, wrap with
 *       \c extern "C" is applied automatically via the \c __cplusplus guard.
 */

#pragma once

#include "ProcessMetrics.h"
#include <cstdint>

#ifdef BENCHMARKCORE_EXPORTS
    #define BENCHMARK_API __declspec(dllexport)
#else
    #define BENCHMARK_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Opaque handle to a BenchmarkEngine instance.
 *
 * Callers treat this as a token — create with BenchmarkCreate,
 * destroy with BenchmarkDestroy. Do not dereference.
 */
typedef void* BenchmarkHandle;

/**
 * @brief Creates a new benchmark session for the given process.
 *
 * @param processId  The OS process identifier to monitor.
 * @param intervalMs Sampling interval in milliseconds.
 * @return A valid BenchmarkHandle on success, NULL on failure
 *         (e.g., access denied, PID not found).
 */
BENCHMARK_API BenchmarkHandle BenchmarkCreate(uint32_t processId, uint32_t intervalMs);

/**
 * @brief Starts background metric collection.
 *
 * @param handle A valid handle returned by BenchmarkCreate.
 * @return 0 on success, non-zero on failure.
 */
BENCHMARK_API int32_t BenchmarkStart(BenchmarkHandle handle);

/**
 * @brief Stops background metric collection.
 *
 * Blocks until the background thread has joined. Safe to call
 * multiple times or on a handle that was never started.
 *
 * @param handle A valid handle returned by BenchmarkCreate.
 */
BENCHMARK_API void BenchmarkStop(BenchmarkHandle handle);

/**
 * @brief Destroys a benchmark session and frees all associated resources.
 *
 * Implicitly calls BenchmarkStop if the engine is still running.
 * The handle is invalid after this call.
 *
 * @param handle A valid handle returned by BenchmarkCreate (may be NULL).
 */
BENCHMARK_API void BenchmarkDestroy(BenchmarkHandle handle);

/**
 * @brief Retrieves the most recently collected metrics snapshot.
 *
 * @param handle      A valid handle returned by BenchmarkCreate.
 * @param outSnapshot Caller-allocated struct to receive the snapshot.
 * @return 0 on success, non-zero if no sample has been taken yet or handle is NULL.
 */
BENCHMARK_API int32_t BenchmarkGetSnapshot(
    BenchmarkHandle         handle,
    ProcessMetricsSnapshot* outSnapshot);

/**
 * @brief Enumerates all running process IDs.
 *
 * @param outPids  Caller-allocated array of at least @p maxCount elements.
 * @param maxCount Capacity of @p outPids.
 * @param outCount Set to the number of PIDs actually written.
 * @return 0 on success, non-zero on failure.
 */
BENCHMARK_API int32_t BenchmarkEnumerateProcesses(
    uint32_t* outPids,
    uint32_t  maxCount,
    uint32_t* outCount);

/**
 * @brief Retrieves the executable (base) name for the given PID.
 *
 * @param pid       Process identifier to query.
 * @param outName   Caller-allocated char buffer (UTF-8).
 * @param bufferLen Byte length of @p outName (including null terminator).
 * @return 0 on success; non-zero means the name could not be retrieved
 *         and @p outName is set to a placeholder string.
 */
BENCHMARK_API int32_t BenchmarkGetProcessName(
    uint32_t pid,
    char*    outName,
    uint32_t bufferLen);

/**
 * @brief Resets all accumulated statistics (averages, peaks) for a session.
 *
 * @param handle A valid handle returned by BenchmarkCreate.
 */
BENCHMARK_API void BenchmarkReset(BenchmarkHandle handle);

#ifdef __cplusplus
} // extern "C"
#endif
