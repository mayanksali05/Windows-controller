#include "UnlockState.h"
#include "log.h"

namespace
{
    const DWORD kMagic = 0x31554C57; // "WLU1" little-endian
    const DWORD kReadTimeoutMs = 5000;
    const wchar_t kSystemSid[] = L"S-1-5-18";

    // Owner SYSTEM: the WinLock service refuses to send a password to a pipe
    // whose owner is not SYSTEM, so an ordinary process cannot impersonate
    // this endpoint (setting another principal as owner needs privileges).
    // Only SYSTEM and interactively logged-on users may connect, and the
    // provider additionally checks that the connecting user IS the locked user.
    const wchar_t kPipeSddl[] = L"O:SYG:SYD:P(A;;GA;;;SY)(A;;GRGW;;;IU)";

    class SrwExclusive
    {
    public:
        explicit SrwExclusive(SRWLOCK* lock) : _lock(lock) { AcquireSRWLockExclusive(_lock); }
        ~SrwExclusive() { ReleaseSRWLockExclusive(_lock); }
        SrwExclusive(const SrwExclusive&) = delete;
        SrwExclusive& operator=(const SrwExclusive&) = delete;
    private:
        SRWLOCK* _lock;
    };

    void SecureClear(std::wstring& value)
    {
        if (!value.empty())
        {
            SecureZeroMemory(&value[0], value.size() * sizeof(wchar_t));
        }
        value.clear();
    }

    // Waits for an overlapped operation, a stop request, or a timeout.
    // Returns true when the I/O completed successfully.
    bool WaitIo(HANDLE pipe, OVERLAPPED& ov, HANDLE stop, DWORD timeoutMs, DWORD& transferred)
    {
        HANDLE handles[2] = { stop, ov.hEvent };
        DWORD wait = WaitForMultipleObjects(2, handles, FALSE, timeoutMs);
        if (wait != WAIT_OBJECT_0 + 1)
        {
            CancelIo(pipe);
            GetOverlappedResult(pipe, &ov, &transferred, TRUE);
            return false;
        }
        return GetOverlappedResult(pipe, &ov, &transferred, FALSE) != FALSE;
    }
}

UnlockState::UnlockState()
{
    _resultEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    _stopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
}

UnlockState::~UnlockState()
{
    StopPipeServer();
    SetEvents(nullptr, 0);
    {
        SrwExclusive guard(&_lock);
        ClearPendingLocked();
    }
    if (_resultEvent) CloseHandle(_resultEvent);
    if (_stopEvent) CloseHandle(_stopEvent);
}

void UnlockState::SetEvents(ICredentialProviderEvents* events, UINT_PTR adviseContext)
{
    ICredentialProviderEvents* previous = nullptr;
    {
        SrwExclusive guard(&_lock);
        previous = _events;
        _events = events;
        _adviseContext = adviseContext;
        if (_events)
        {
            _events->AddRef();
        }
    }
    if (previous)
    {
        previous->Release();
    }
}

void UnlockState::SetUsers(std::vector<LockedUser> users)
{
    SrwExclusive guard(&_lock);
    _users = std::move(users);
}

void UnlockState::StartPipeServer()
{
    if (_thread || !_stopEvent || !_resultEvent)
    {
        return;
    }

    ResetEvent(_stopEvent);
    // The thread keeps the state (and the DLL) alive until it exits.
    auto* keepAlive = new (std::nothrow) std::shared_ptr<UnlockState>(shared_from_this());
    if (!keepAlive)
    {
        return;
    }

    DllAddRef();
    _thread = CreateThread(nullptr, 0, PipeThreadProc, keepAlive, 0, nullptr);
    if (!_thread)
    {
        delete keepAlive;
        DllRelease();
    }
}

void UnlockState::StopPipeServer()
{
    if (!_thread)
    {
        return;
    }

    SetEvent(_stopEvent);
    // Never block LogonUI for long. If the thread is stuck it still holds its
    // own references, so the state and the DLL stay valid until it exits.
    if (GetCurrentThreadId() != GetThreadId(_thread))
    {
        WaitForSingleObject(_thread, 3000);
    }
    CloseHandle(_thread);
    _thread = nullptr;
}

DWORD WINAPI UnlockState::PipeThreadProc(LPVOID parameter)
{
    auto* keepAlive = static_cast<std::shared_ptr<UnlockState>*>(parameter);
    (*keepAlive)->RunPipeServer();
    delete keepAlive;
    DllRelease();
    return 0;
}

void UnlockState::RunPipeServer()
{
    PSECURITY_DESCRIPTOR descriptor = nullptr;
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
            kPipeSddl, SDDL_REVISION_1, &descriptor, nullptr))
    {
        return;
    }

    SECURITY_ATTRIBUTES attributes = { sizeof(attributes), descriptor, FALSE };
    WinLockLog(L"Pipe: server thread started");
    HANDLE ioEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);

    while (ioEvent && WaitForSingleObject(_stopEvent, 0) != WAIT_OBJECT_0)
    {
        HANDLE pipe = CreateNamedPipeW(
            WINLOCK_PIPE_NAME,
            PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            1, 64, 1024, 0, &attributes);

        if (pipe == INVALID_HANDLE_VALUE)
        {
            WinLockLog(L"Pipe: CreateNamedPipe failed error=%lu", GetLastError());
            // Another instance owns the name (or a transient failure); retry later.
            WaitForSingleObject(_stopEvent, 2000);
            continue;
        }

        OVERLAPPED ov = {};
        ov.hEvent = ioEvent;
        ResetEvent(ioEvent);

        bool connected = false;
        if (ConnectNamedPipe(pipe, &ov))
        {
            connected = true;
        }
        else
        {
            DWORD error = GetLastError();
            if (error == ERROR_PIPE_CONNECTED)
            {
                connected = true;
            }
            else if (error == ERROR_IO_PENDING)
            {
                DWORD ignored = 0;
                connected = WaitIo(pipe, ov, _stopEvent, INFINITE, ignored);
            }
        }

        if (connected)
        {
            HandleClient(pipe);
            FlushFileBuffers(pipe);
            DisconnectNamedPipe(pipe);
        }
        CloseHandle(pipe);
    }

    if (ioEvent) CloseHandle(ioEvent);
    LocalFree(descriptor);
}

bool UnlockState::ReadRequest(HANDLE pipe, std::wstring& password)
{
    BYTE buffer[8 + WINLOCK_MAX_PASSWORD_BYTES];
    HANDLE ioEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!ioEvent)
    {
        return false;
    }

    OVERLAPPED ov = {};
    ov.hEvent = ioEvent;
    DWORD read = 0;
    bool ok = false;
    if (ReadFile(pipe, buffer, sizeof(buffer), &read, &ov))
    {
        ok = GetOverlappedResult(pipe, &ov, &read, FALSE) != FALSE;
    }
    else if (GetLastError() == ERROR_IO_PENDING)
    {
        ok = WaitIo(pipe, ov, _stopEvent, kReadTimeoutMs, read);
    }
    CloseHandle(ioEvent);

    bool valid = false;
    if (ok && read >= 8)
    {
        DWORD magic = 0;
        DWORD length = 0;
        CopyMemory(&magic, buffer, 4);
        CopyMemory(&length, buffer + 4, 4);
        if (magic == kMagic && length > 0 && length <= WINLOCK_MAX_PASSWORD_BYTES &&
            (length % sizeof(wchar_t)) == 0 && read == 8 + length)
        {
            try
            {
                password.assign(reinterpret_cast<const wchar_t*>(buffer + 8), length / sizeof(wchar_t));
                valid = password.find(L'\0') == std::wstring::npos;
            }
            catch (...)
            {
                valid = false;
            }
        }
    }

    SecureZeroMemory(buffer, sizeof(buffer));
    if (!valid)
    {
        SecureClear(password);
    }
    return valid;
}

bool UnlockState::GetClientSid(HANDLE pipe, std::wstring& sid)
{
    if (!ImpersonateNamedPipeClient(pipe))
    {
        return false;
    }

    HANDLE token = nullptr;
    BOOL opened = OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &token);
    RevertToSelf();
    if (!opened)
    {
        return false;
    }

    bool ok = false;
    DWORD size = 0;
    GetTokenInformation(token, TokenUser, nullptr, 0, &size);
    if (size > 0)
    {
        std::vector<BYTE> info(size);
        if (GetTokenInformation(token, TokenUser, info.data(), size, &size))
        {
            auto* user = reinterpret_cast<TOKEN_USER*>(info.data());
            LPWSTR text = nullptr;
            if (ConvertSidToStringSidW(user->User.Sid, &text))
            {
                sid = text;
                LocalFree(text);
                ok = true;
            }
        }
    }
    CloseHandle(token);
    return ok;
}

void UnlockState::WriteResponse(HANDLE pipe, UnlockResultCode code, NTSTATUS status)
{
    BYTE response[5];
    response[0] = static_cast<BYTE>(code);
    CopyMemory(response + 1, &status, 4);

    HANDLE ioEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!ioEvent)
    {
        return;
    }
    OVERLAPPED ov = {};
    ov.hEvent = ioEvent;
    DWORD written = 0;
    if (!WriteFile(pipe, response, sizeof(response), &written, &ov) &&
        GetLastError() == ERROR_IO_PENDING)
    {
        // Ignore the stop event here: the response must go out even during
        // teardown, so wait on the I/O only (bounded).
        WaitForSingleObject(ioEvent, 2000);
        if (!HasOverlappedIoCompleted(&ov))
        {
            CancelIo(pipe);
        }
        GetOverlappedResult(pipe, &ov, &written, TRUE);
    }
    CloseHandle(ioEvent);
}

void UnlockState::HandleClient(HANDLE pipe)
{
    std::wstring password;
    std::wstring clientSid;

    WinLockLog(L"Pipe: client connected");
    if (!ReadRequest(pipe, password) || !GetClientSid(pipe, clientSid))
    {
        WinLockLog(L"Pipe: bad request");
        SecureClear(password);
        WriteResponse(pipe, UNLOCK_RESULT_BAD_REQUEST, 0);
        return;
    }

    NTSTATUS status = 0;
    UnlockResultCode code = Submit(clientSid, password, status);
    SecureClear(password);
    WinLockLog(L"Pipe: request from %s -> code=%d status=0x%08X", clientSid.c_str(),
        static_cast<int>(code), static_cast<unsigned>(status));
    WriteResponse(pipe, code, status);
}

UnlockResultCode UnlockState::Submit(const std::wstring& clientSid, std::wstring& password, NTSTATUS& status)
{
    status = 0;
    {
        SrwExclusive guard(&_lock);
        if (_pending)
        {
            return UNLOCK_RESULT_BUSY;
        }

        // The caller must be the locked user itself (the WinLock service runs
        // in that user's session). SYSTEM is accepted only when exactly one
        // user is locked, for a future service-hosted deployment.
        const LockedUser* target = nullptr;
        for (const auto& user : _users)
        {
            if (_wcsicmp(user.sid.c_str(), clientSid.c_str()) == 0)
            {
                target = &user;
                break;
            }
        }
        if (!target && _wcsicmp(clientSid.c_str(), kSystemSid) == 0 && _users.size() == 1)
        {
            target = &_users[0];
        }
        if (!target)
        {
            return UNLOCK_RESULT_NO_USER;
        }

        try
        {
            _targetSid = target->sid;
            _password.swap(password);
        }
        catch (...)
        {
            return UNLOCK_RESULT_BAD_REQUEST;
        }
        _pending = true;
        _handedOut = false;
        _resultCode = UNLOCK_RESULT_TIMEOUT;
        _resultStatus = 0;
        ResetEvent(_resultEvent);
    }

    // Ask LogonUI to re-enumerate: GetCredentialCount will now report our
    // credential as the default with auto-logon, and LogonUI will call
    // GetSerialization on it.
    NotifyCredentialsChanged();

    HANDLE handles[2] = { _resultEvent, _stopEvent };
    DWORD wait = WaitForMultipleObjects(2, handles, FALSE, WINLOCK_RESULT_TIMEOUT_MS);

    UnlockResultCode code;
    bool timedOut = false;
    {
        SrwExclusive guard(&_lock);
        if (wait == WAIT_OBJECT_0)
        {
            code = _resultCode;
            status = _resultStatus;
        }
        else if (wait == WAIT_OBJECT_0 + 1)
        {
            // LogonUI is tearing the provider down. If our credential was
            // already handed to Windows, that teardown is the session
            // unlocking; the service double-checks the desktop state anyway.
            code = _handedOut ? UNLOCK_RESULT_SUCCESS : UNLOCK_RESULT_TIMEOUT;
        }
        else
        {
            code = UNLOCK_RESULT_TIMEOUT;
            timedOut = true;
        }
        ClearPendingLocked();
    }

    if (timedOut)
    {
        // Drop the auto-logon flag from the tile.
        NotifyCredentialsChanged();
    }
    return code;
}

void UnlockState::NotifyCredentialsChanged()
{
    ICredentialProviderEvents* events = nullptr;
    UINT_PTR context = 0;
    {
        SrwExclusive guard(&_lock);
        events = _events;
        context = _adviseContext;
        if (events)
        {
            events->AddRef();
        }
    }
    if (events)
    {
        events->CredentialsChanged(context);
        events->Release();
    }
}

void UnlockState::ClearPendingLocked()
{
    SecureClear(_password);
    _targetSid.clear();
    _pending = false;
    _handedOut = false;
}

bool UnlockState::HasPendingFor(const std::wstring& sid)
{
    SrwExclusive guard(&_lock);
    return _pending && !_handedOut && _wcsicmp(_targetSid.c_str(), sid.c_str()) == 0;
}

bool UnlockState::TakePassword(const std::wstring& sid, std::wstring& password)
{
    SrwExclusive guard(&_lock);
    if (!_pending || _handedOut || _wcsicmp(_targetSid.c_str(), sid.c_str()) != 0)
    {
        return false;
    }
    try
    {
        password.assign(_password);
    }
    catch (...)
    {
        return false;
    }
    SecureClear(_password);
    _handedOut = true;
    return true;
}

bool UnlockState::ReportResult(const std::wstring& sid, NTSTATUS status)
{
    SrwExclusive guard(&_lock);
    if (!_pending || !_handedOut || _wcsicmp(_targetSid.c_str(), sid.c_str()) != 0)
    {
        return false;
    }
    _resultCode = NT_SUCCESS(status) ? UNLOCK_RESULT_SUCCESS : UNLOCK_RESULT_REJECTED;
    _resultStatus = status;
    SetEvent(_resultEvent);
    return true;
}

void UnlockState::FailHandoff(const std::wstring& sid)
{
    SrwExclusive guard(&_lock);
    if (_pending && _wcsicmp(_targetSid.c_str(), sid.c_str()) == 0)
    {
        _resultCode = UNLOCK_RESULT_BAD_REQUEST;
        _resultStatus = 0;
        SetEvent(_resultEvent);
    }
}
