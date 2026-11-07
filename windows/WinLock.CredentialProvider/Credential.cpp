#include "Credential.h"
#include "helpers.h"
#include "resource.h"
#include "log.h"

namespace
{
    const wchar_t kLabel[] = L"WinLock: unlock with your iPhone";
    const wchar_t kLargeText[] = L"Unlock with iPhone";
    const wchar_t kWaiting[] = L"Open WinLock on your iPhone and tap Unlock.";
    const wchar_t kRejected[] =
        L"Windows rejected the password sent from your iPhone. "
        L"Update it in the WinLock app (Laptop \x2192 Unlock password).";
}

bool GetWinLockFieldDescriptor(DWORD fieldId, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR& descriptor)
{
    descriptor = {};
    descriptor.dwFieldID = fieldId;
    switch (fieldId)
    {
    case FI_TILEIMAGE:
        descriptor.cpft = CPFT_TILE_IMAGE;
        descriptor.pszLabel = const_cast<LPWSTR>(L"Image");
        descriptor.guidFieldType = WINLOCK_CPFG_LOGO;
        return true;
    case FI_LABEL:
        descriptor.cpft = CPFT_SMALL_TEXT;
        descriptor.pszLabel = const_cast<LPWSTR>(L"Tooltip");
        descriptor.guidFieldType = WINLOCK_CPFG_LABEL;
        return true;
    case FI_LARGE_TEXT:
        descriptor.cpft = CPFT_LARGE_TEXT;
        descriptor.pszLabel = const_cast<LPWSTR>(L"WinLock");
        return true;
    case FI_STATUS:
        descriptor.cpft = CPFT_SMALL_TEXT;
        descriptor.pszLabel = const_cast<LPWSTR>(L"Status");
        return true;
    case FI_SUBMIT:
        descriptor.cpft = CPFT_SUBMIT_BUTTON;
        descriptor.pszLabel = const_cast<LPWSTR>(L"Submit");
        return true;
    default:
        return false;
    }
}

HRESULT WinLockCredential::Create(
    CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    const LockedUser& user,
    std::shared_ptr<UnlockState> state,
    WinLockCredential** credential)
{
    *credential = nullptr;
    try
    {
        *credential = new WinLockCredential(scenario, user, std::move(state));
        return S_OK;
    }
    catch (...)
    {
        return E_OUTOFMEMORY;
    }
}

WinLockCredential::WinLockCredential(
    CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    LockedUser user,
    std::shared_ptr<UnlockState> state)
    : _scenario(scenario), _user(std::move(user)), _state(std::move(state))
{
    DllAddRef();
}

WinLockCredential::~WinLockCredential()
{
    if (_events)
    {
        _events->Release();
    }
    DllRelease();
}

IFACEMETHODIMP WinLockCredential::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }
    if (riid == __uuidof(IUnknown) ||
        riid == __uuidof(ICredentialProviderCredential) ||
        riid == __uuidof(ICredentialProviderCredential2))
    {
        *ppv = static_cast<ICredentialProviderCredential2*>(this);
        AddRef();
        return S_OK;
    }
    WinLockLogGuid(L"Credential::QueryInterface: not supported", riid);
    *ppv = nullptr;
    return E_NOINTERFACE;
}

IFACEMETHODIMP_(ULONG) WinLockCredential::AddRef()
{
    return InterlockedIncrement(&_refCount);
}

IFACEMETHODIMP_(ULONG) WinLockCredential::Release()
{
    LONG count = InterlockedDecrement(&_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

IFACEMETHODIMP WinLockCredential::Advise(ICredentialProviderCredentialEvents* events)
{
    if (_events)
    {
        _events->Release();
    }
    _events = events;
    if (_events)
    {
        _events->AddRef();
    }
    return S_OK;
}

IFACEMETHODIMP WinLockCredential::UnAdvise()
{
    if (_events)
    {
        _events->Release();
        _events = nullptr;
    }
    return S_OK;
}

IFACEMETHODIMP WinLockCredential::SetSelected(BOOL* autoLogon)
{
    *autoLogon = _state->HasPendingFor(_user.sid) ? TRUE : FALSE;
    WinLockLog(L"Credential::SetSelected autoLogon=%d", *autoLogon);
    return S_OK;
}

IFACEMETHODIMP WinLockCredential::SetDeselected()
{
    return S_OK;
}

IFACEMETHODIMP WinLockCredential::GetFieldState(
    DWORD fieldId,
    CREDENTIAL_PROVIDER_FIELD_STATE* state,
    CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE* interactiveState)
{
    if (!state || !interactiveState)
    {
        return E_INVALIDARG;
    }

    *interactiveState = CPFIS_NONE;
    switch (fieldId)
    {
    case FI_TILEIMAGE:
        *state = CPFS_DISPLAY_IN_BOTH;
        return S_OK;
    case FI_LABEL:
        *state = CPFS_HIDDEN;
        return S_OK;
    case FI_LARGE_TEXT:
    case FI_STATUS:
    case FI_SUBMIT:
        *state = CPFS_DISPLAY_IN_SELECTED_TILE;
        return S_OK;
    default:
        return E_INVALIDARG;
    }
}

IFACEMETHODIMP WinLockCredential::GetStringValue(DWORD fieldId, LPWSTR* value)
{
    if (!value)
    {
        return E_INVALIDARG;
    }
    *value = nullptr;
    switch (fieldId)
    {
    case FI_LABEL:
        return SHStrDupW(kLabel, value);
    case FI_LARGE_TEXT:
        return SHStrDupW(kLargeText, value);
    case FI_STATUS:
        return SHStrDupW(kWaiting, value);
    default:
        return E_INVALIDARG;
    }
}

IFACEMETHODIMP WinLockCredential::GetBitmapValue(DWORD fieldId, HBITMAP* bitmap)
{
    if (!bitmap)
    {
        return E_INVALIDARG;
    }
    *bitmap = nullptr;
    if (fieldId != FI_TILEIMAGE)
    {
        return E_INVALIDARG;
    }
    HBITMAP loaded = LoadBitmapW(g_hinst, MAKEINTRESOURCEW(IDB_TILE_IMAGE));
    if (!loaded)
    {
        HRESULT hr = HRESULT_FROM_WIN32(GetLastError());
        WinLockLog(L"Credential::GetBitmapValue LoadBitmap failed hr=0x%08X", hr);
        return hr;
    }
    WinLockLog(L"Credential::GetBitmapValue ok");
    *bitmap = loaded;
    return S_OK;
}

IFACEMETHODIMP WinLockCredential::GetCheckboxValue(DWORD, BOOL*, LPWSTR*)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP WinLockCredential::GetSubmitButtonValue(DWORD fieldId, DWORD* adjacentTo)
{
    if (fieldId != FI_SUBMIT || !adjacentTo)
    {
        return E_INVALIDARG;
    }
    *adjacentTo = FI_STATUS;
    return S_OK;
}

IFACEMETHODIMP WinLockCredential::GetComboBoxValueCount(DWORD, DWORD*, DWORD*)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP WinLockCredential::GetComboBoxValueAt(DWORD, DWORD, LPWSTR*)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP WinLockCredential::SetStringValue(DWORD, LPCWSTR)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP WinLockCredential::SetCheckboxValue(DWORD, BOOL)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP WinLockCredential::SetComboBoxSelectedValue(DWORD, DWORD)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP WinLockCredential::CommandLinkClicked(DWORD)
{
    return E_NOTIMPL;
}

void WinLockCredential::SplitQualifiedName(std::wstring& domain, std::wstring& userName) const
{
    // "DOMAIN\user" (local / domain / "MicrosoftAccount\me@x.com") or a UPN.
    const std::wstring& name = _user.qualifiedName;
    size_t slash = name.find(L'\\');
    if (slash != std::wstring::npos)
    {
        domain = name.substr(0, slash);
        userName = name.substr(slash + 1);
    }
    else
    {
        domain.clear();
        userName = name;
    }
}

IFACEMETHODIMP WinLockCredential::GetSerialization(
    CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE* response,
    CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* serialization,
    LPWSTR* optionalStatusText,
    CREDENTIAL_PROVIDER_STATUS_ICON* optionalStatusIcon)
{
    if (!response || !serialization || !optionalStatusText || !optionalStatusIcon)
    {
        return E_INVALIDARG;
    }

    *response = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
    *optionalStatusText = nullptr;
    *optionalStatusIcon = CPSI_NONE;
    ZeroMemory(serialization, sizeof(*serialization));

    std::wstring password;
    if (!_state->TakePassword(_user.sid, password))
    {
        WinLockLog(L"Credential::GetSerialization: no pending iPhone request");
        // Tile clicked by hand without a pending iPhone request.
        SHStrDupW(kWaiting, optionalStatusText);
        return S_OK;
    }

    HRESULT hr = E_FAIL;
    PWSTR protectedPassword = nullptr;
    try
    {
        if (_user.isLocal)
        {
            // Local account: KERB_INTERACTIVE_UNLOCK_LOGON with the computer
            // name as the domain (as in Microsoft's SampleV2CredentialProvider).
            std::wstring domain;
            std::wstring userName;
            SplitQualifiedName(domain, userName);

            WCHAR computerName[MAX_COMPUTERNAME_LENGTH + 1];
            DWORD computerNameLength = ARRAYSIZE(computerName);
            if (GetComputerNameW(computerName, &computerNameLength))
            {
                domain = computerName;
            }

            hr = ProtectIfNecessaryAndCopyPassword(password.c_str(), _scenario, &protectedPassword);
            if (SUCCEEDED(hr))
            {
                KERB_INTERACTIVE_UNLOCK_LOGON logon;
                hr = KerbInteractiveUnlockLogonInit(
                    &domain[0], &userName[0], protectedPassword, _scenario, &logon);
                if (SUCCEEDED(hr))
                {
                    hr = KerbInteractiveUnlockLogonPack(
                        logon, &serialization->rgbSerialization, &serialization->cbSerialization);
                }
            }
        }
        else
        {
            // Microsoft account / domain account: let CredPackAuthenticationBuffer
            // build the identity-provider credential for the qualified name.
            const DWORD flags = CRED_PACK_PROTECTED_CREDENTIALS | CRED_PACK_ID_PROVIDER_CREDENTIALS;
            PWSTR qualifiedName = const_cast<PWSTR>(_user.qualifiedName.c_str());
            PWSTR plain = &password[0];
            DWORD size = 0;
            if (!CredPackAuthenticationBufferW(flags, qualifiedName, plain, nullptr, &size) &&
                GetLastError() == ERROR_INSUFFICIENT_BUFFER && size > 0)
            {
                serialization->rgbSerialization = static_cast<BYTE*>(CoTaskMemAlloc(size));
                if (!serialization->rgbSerialization)
                {
                    hr = E_OUTOFMEMORY;
                }
                else if (CredPackAuthenticationBufferW(
                             flags, qualifiedName, plain, serialization->rgbSerialization, &size))
                {
                    serialization->cbSerialization = size;
                    hr = S_OK;
                }
                else
                {
                    hr = HRESULT_FROM_WIN32(GetLastError());
                }
            }
            else
            {
                hr = HRESULT_FROM_WIN32(GetLastError());
                if (SUCCEEDED(hr))
                {
                    hr = E_FAIL;
                }
            }
        }

        if (SUCCEEDED(hr))
        {
            ULONG authPackage = 0;
            hr = RetrieveNegotiateAuthPackage(&authPackage);
            if (SUCCEEDED(hr))
            {
                serialization->ulAuthenticationPackage = authPackage;
                serialization->clsidCredentialProvider = CLSID_WinLockProvider;
                *response = CPGSR_RETURN_CREDENTIAL_FINISHED;
            }
        }
    }
    catch (...)
    {
        hr = E_OUTOFMEMORY;
    }

    if (!password.empty())
    {
        SecureZeroMemory(&password[0], password.size() * sizeof(wchar_t));
    }
    CoTaskMemFreeSecure(protectedPassword);

    if (FAILED(hr))
    {
        if (serialization->rgbSerialization)
        {
            SecureZeroMemory(serialization->rgbSerialization, serialization->cbSerialization);
            CoTaskMemFree(serialization->rgbSerialization);
            serialization->rgbSerialization = nullptr;
            serialization->cbSerialization = 0;
        }
        *response = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
        _state->FailHandoff(_user.sid);
    }
    WinLockLog(L"Credential::GetSerialization local=%d hr=0x%08X response=%d",
        _user.isLocal ? 1 : 0, hr, static_cast<int>(*response));
    return hr;
}

IFACEMETHODIMP WinLockCredential::ReportResult(
    NTSTATUS status,
    NTSTATUS,
    LPWSTR* optionalStatusText,
    CREDENTIAL_PROVIDER_STATUS_ICON* optionalStatusIcon)
{
    if (optionalStatusText)
    {
        *optionalStatusText = nullptr;
    }
    if (optionalStatusIcon)
    {
        *optionalStatusIcon = CPSI_NONE;
    }

    bool ours = _state->ReportResult(_user.sid, status);
    WinLockLog(L"Credential::ReportResult status=0x%08X ours=%d", static_cast<unsigned>(status), ours ? 1 : 0);
    if (ours && !NT_SUCCESS(status) && optionalStatusText && optionalStatusIcon)
    {
        if (SUCCEEDED(SHStrDupW(kRejected, optionalStatusText)))
        {
            *optionalStatusIcon = CPSI_ERROR;
        }
    }
    return S_OK;
}

IFACEMETHODIMP WinLockCredential::GetUserSid(LPWSTR* sid)
{
    if (!sid)
    {
        return E_INVALIDARG;
    }
    return SHStrDupW(_user.sid.c_str(), sid);
}
