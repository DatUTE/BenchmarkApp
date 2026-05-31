/**
 * @file HandleGuard.h
 * @brief RAII wrapper for Win32 HANDLE resources.
 *
 * Provides automatic resource management for Win32 kernel objects,
 * eliminating handle leaks through strict RAII ownership semantics.
 * Non-copyable; movable.
 */

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <utility>

namespace Benchmark::Core
{

/**
 * @brief Owns a Win32 HANDLE and closes it on destruction (RAII pattern).
 *
 * Usage:
 * @code
 *   HandleGuard proc{ OpenProcess(PROCESS_QUERY_INFORMATION, FALSE, pid) };
 *   if (!proc.isValid()) { // handle error }
 *   // proc.get() can now be used safely
 *   // HANDLE is closed automatically when proc goes out of scope
 * @endcode
 */
class HandleGuard final
{
public:
    /**
     * @brief Constructs a guard that owns the given handle.
     * @param handle The Win32 HANDLE to own. Passing INVALID_HANDLE_VALUE or NULL
     *               is valid — isValid() will return false.
     */
    explicit HandleGuard(HANDLE handle = INVALID_HANDLE_VALUE) noexcept
        : handle_{ handle }
    {}

    HandleGuard(const HandleGuard&)            = delete;
    HandleGuard& operator=(const HandleGuard&) = delete;

    /** @brief Move-constructs by transferring ownership from @p other. */
    HandleGuard(HandleGuard&& other) noexcept
        : handle_{ std::exchange(other.handle_, INVALID_HANDLE_VALUE) }
    {}

    /** @brief Move-assigns by transferring ownership from @p other. */
    HandleGuard& operator=(HandleGuard&& other) noexcept
    {
        if (this != &other)
        {
            close();
            handle_ = std::exchange(other.handle_, INVALID_HANDLE_VALUE);
        }
        return *this;
    }

    /** @brief Closes the owned handle (if valid). */
    ~HandleGuard() noexcept { close(); }

    // ── Accessors ───────────────────────────────────────────────────────────

    /** @brief Returns the raw Win32 HANDLE. */
    [[nodiscard]] HANDLE get() const noexcept { return handle_; }

    /**
     * @brief Returns true when the handle is neither NULL nor INVALID_HANDLE_VALUE.
     */
    [[nodiscard]] bool isValid() const noexcept
    {
        return handle_ != nullptr && handle_ != INVALID_HANDLE_VALUE;
    }

    /**
     * @brief Releases ownership without closing the handle.
     * @return The raw handle; caller is responsible for closing it.
     */
    [[nodiscard]] HANDLE release() noexcept
    {
        return std::exchange(handle_, INVALID_HANDLE_VALUE);
    }

private:
    void close() noexcept
    {
        if (isValid())
        {
            CloseHandle(handle_);
            handle_ = INVALID_HANDLE_VALUE;
        }
    }

    HANDLE handle_;
};

} // namespace Benchmark::Core