#include "log.h"

#include <stdarg.h>

namespace
{
    const LONGLONG kMaxLogBytes = 512 * 1024;
    SRWLOCK g_logLock = SRWLOCK_INIT;

    bool GetLogPath(wchar_t* path, size_t capacity)
    {
        wchar_t directory[MAX_PATH];
        DWORD length = ExpandEnvironmentStringsW(L"%ProgramData%\\WinLock", directory, ARRAYSIZE(directory));
        if (length == 0 || length > ARRAYSIZE(directory))
        {
            return false;
        }
        CreateDirectoryW(directory, nullptr); // fine if it already exists
        return SUCCEEDED(StringCchPrintfW(path, capacity, L"%s\\provider.log", directory));
    }
}

void WinLockLog(const wchar_t* format, ...)
{
    DWORD savedError = GetLastError();

    wchar_t message[1024];
    va_list args;
    va_start(args, format);
    StringCchVPrintfW(message, ARRAYSIZE(message), format, args);
    va_end(args);

    SYSTEMTIME now;
    GetLocalTime(&now);
    wchar_t line[1200];
    StringCchPrintfW(line, ARRAYSIZE(line), L"%04u-%02u-%02u %02u:%02u:%02u.%03u pid=%lu tid=%lu %s\r\n",
        now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond, now.wMilliseconds,
        GetCurrentProcessId(), GetCurrentThreadId(), message);

    char utf8[2400];
    int bytes = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8), nullptr, nullptr);

    wchar_t path[MAX_PATH];
    if (bytes > 1 && GetLogPath(path, ARRAYSIZE(path)))
    {
        AcquireSRWLockExclusive(&g_logLock);
        HANDLE file = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file != INVALID_HANDLE_VALUE)
        {
            LARGE_INTEGER size;
            if (GetFileSizeEx(file, &size) && size.QuadPart > kMaxLogBytes)
            {
                CloseHandle(file);
                file = CreateFileW(path, GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
            }
            if (file != INVALID_HANDLE_VALUE)
            {
                DWORD written = 0;
                WriteFile(file, utf8, static_cast<DWORD>(bytes - 1), &written, nullptr);
                CloseHandle(file);
            }
        }
        ReleaseSRWLockExclusive(&g_logLock);
    }

    SetLastError(savedError);
}

void WinLockLogGuid(const wchar_t* prefix, REFGUID guid)
{
    wchar_t text[64] = L"?";
    StringFromGUID2(guid, text, ARRAYSIZE(text));
    WinLockLog(L"%s %s", prefix, text);
}
