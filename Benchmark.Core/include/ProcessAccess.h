/**
 * @file ProcessAccess.h
 * @brief Helpers for opening process handles across integrity levels.
 */

#pragma once

#include <cstdint>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

namespace Benchmark::Core
{

/// Enables SeDebugPrivilege once per process (no-op if unavailable).
void ensureDebugPrivilegeEnabled() noexcept;

/// Opens a process with the broadest query access available.
/// Returns nullptr if the PID does not exist or access is denied.
[[nodiscard]] HANDLE openProcessForMonitoring(uint32_t processId) noexcept;

/// Writes the executable base name for @p processId into @p outName.
/// Returns 0 on success, -1 on failure.
[[nodiscard]] int32_t queryProcessBaseName(uint32_t processId, char* outName, uint32_t bufferLen) noexcept;

} // namespace Benchmark::Core
