// Shared state between the provider, its credentials, and the named-pipe
// thread that receives unlock requests from WinLock.Service.
#pragma once

#include "common.h"

#include <memory>
#include <string>
#include <vector>

struct LockedUser
{
    std::wstring sid;
    std::wstring qualifiedName; // "DOMAIN\user", "MicrosoftAccount\me@x.com", or a UPN
    bool isLocal = false;       // local account (vs Microsoft/domain account)
};

class UnlockState : public std::enable_shared_from_this<UnlockState>
{
public:
    UnlockState();
    ~UnlockState();

    UnlockState(const UnlockState&) = delete;
    UnlockState& operator=(const UnlockState&) = delete;

    // --- provider side -----------------------------------------------------
    void SetEvents(ICredentialProviderEvents* events, UINT_PTR adviseContext);
    void SetUsers(std::vector<LockedUser> users);

    // Starts/stops the pipe server thread (idempotent).
    void StartPipeServer();
    void StopPipeServer();

    // --- credential side ---------------------------------------------------
    // True while an iPhone request for this user waits to be serialized.
    bool HasPendingFor(const std::wstring& sid);
    // Moves the pending password out (once). Caller must SecureZero it.
    bool TakePassword(const std::wstring& sid, std::wstring& password);
    // Reports the logon outcome for a password previously taken.
    bool ReportResult(const std::wstring& sid, NTSTATUS status);
    // Serialization failed after TakePassword; release the waiting request.
    void FailHandoff(const std::wstring& sid);

private:
    static DWORD WINAPI PipeThreadProc(LPVOID parameter);
    void RunPipeServer();
    void HandleClient(HANDLE pipe);
    bool ReadRequest(HANDLE pipe, std::wstring& password);
    bool GetClientSid(HANDLE pipe, std::wstring& sid);
    void WriteResponse(HANDLE pipe, UnlockResultCode code, NTSTATUS status);
    UnlockResultCode Submit(const std::wstring& clientSid, std::wstring& password, NTSTATUS& status);
    void NotifyCredentialsChanged();
    void ClearPendingLocked();

    SRWLOCK _lock = SRWLOCK_INIT;

    ICredentialProviderEvents* _events = nullptr;
    UINT_PTR _adviseContext = 0;
    std::vector<LockedUser> _users;

    // Pending request (guarded by _lock).
    bool _pending = false;
    bool _handedOut = false;
    std::wstring _targetSid;
    std::wstring _password;
    UnlockResultCode _resultCode = UNLOCK_RESULT_TIMEOUT;
    NTSTATUS _resultStatus = 0;

    HANDLE _resultEvent = nullptr; // manual reset
    HANDLE _stopEvent = nullptr;   // manual reset
    HANDLE _thread = nullptr;
};
