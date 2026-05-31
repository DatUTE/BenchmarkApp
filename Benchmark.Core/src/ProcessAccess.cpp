/**
 * @file ProcessAccess.cpp
 * @brief Process handle and name query helpers.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <psapi.h>

#pragma comment(lib, "psapi.lib")

#include "ProcessAccess.h"
#include "HandleGuard.h"

#include <cstdio>
#include <cstring>

namespace Benchmark::Core
{

namespace
{

void copyName(char* outName, uint32_t bufferLen, const char* value) noexcept
{
    if (!outName || bufferLen == 0)
        return;

    std::strncpy(outName, value, bufferLen - 1);
    outName[bufferLen - 1] = '\0';
}

void copyNameFromWide(char* outName, uint32_t bufferLen, const wchar_t* path) noexcept
{
    if (!outName || bufferLen == 0 || !path || path[0] == L'\0')
        return;

    const wchar_t* base = path;
    for (const wchar_t* p = path; *p; ++p)
    {
        if (*p == L'\\' || *p == L'/')
            base = p + 1;
    }

    char narrow[MAX_PATH]{};
    const int written = WideCharToMultiByte(
        CP_UTF8, 0, base, -1, narrow, static_cast<int>(sizeof(narrow)), nullptr, nullptr);

    if (written <= 0)
    {
        copyName(outName, bufferLen, "<unknown>");
        return;
    }

    copyName(outName, bufferLen, narrow);
}

} // namespace

void ensureDebugPrivilegeEnabled() noexcept
{
    static bool attempted = false;
    if (attempted)
        return;
    attempted = true;

    HANDLE token = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &token))
        return;

    LUID luid{};
    if (!LookupPrivilegeValueA(nullptr, SE_DEBUG_NAME, &luid))
    {
        CloseHandle(token);
        return;
    }

    TOKEN_PRIVILEGES tp{};
    tp.PrivilegeCount = 1;
    tp.Privileges[0].Luid = luid;
    tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

    AdjustTokenPrivileges(token, FALSE, &tp, sizeof(tp), nullptr, nullptr);
    CloseHandle(token);
}

HANDLE openProcessForMonitoring(uint32_t processId) noexcept
{
    ensureDebugPrivilegeEnabled();

    constexpr DWORD accessLevels[] = {
        PROCESS_QUERY_INFORMATION | PROCESS_VM_READ,
        PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ,
        PROCESS_QUERY_LIMITED_INFORMATION,
    };

    for (DWORD access : accessLevels)
    {
        HANDLE h = OpenProcess(access, FALSE, processId);
        if (h && h != INVALID_HANDLE_VALUE)
            return h;
    }

    return nullptr;
}

int32_t queryProcessBaseName(uint32_t processId, char* outName, uint32_t bufferLen) noexcept
{
    if (!outName || bufferLen == 0)
        return -1;

    outName[0] = '\0';

    HandleGuard guard{ openProcessForMonitoring(processId) };
    if (!guard.isValid())
    {
        copyName(outName, bufferLen, "<unknown>");
        return -1;
    }

    wchar_t imagePath[MAX_PATH]{};
    DWORD size = MAX_PATH;
    if (QueryFullProcessImageNameW(guard.get(), 0, imagePath, &size) && imagePath[0] != L'\0')
    {
        copyNameFromWide(outName, bufferLen, imagePath);
        return 0;
    }

    char name[MAX_PATH]{};
    HMODULE module{};
    DWORD needed = 0;
    if (EnumProcessModules(guard.get(), &module, sizeof(module), &needed))
    {
        if (GetModuleBaseNameA(guard.get(), module, name, sizeof(name)) > 0 && name[0] != '\0')
        {
            copyName(outName, bufferLen, name);
            return 0;
        }
    }

    std::snprintf(name, sizeof(name), "<%u>", processId);
    copyName(outName, bufferLen, name);
    return 0;
}

} // namespace Benchmark::Core
