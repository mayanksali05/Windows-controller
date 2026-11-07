#include "Provider.h"
#include "helpers.h"
#include "log.h"

namespace
{
    // {A198529B-730F-4089-B646-A12557F5665E} Identity_LocalUserProvider (identityprovider.h)
    const GUID kLocalUserProvider =
        { 0xa198529b, 0x730f, 0x4089, { 0xb6, 0x46, 0xa1, 0x25, 0x57, 0xf5, 0x66, 0x5e } };
}

HRESULT WinLockProvider::Create(REFIID riid, void** ppv)
{
    *ppv = nullptr;
    WinLockProvider* provider = nullptr;
    try
    {
        provider = new WinLockProvider();
    }
    catch (...)
    {
        return E_OUTOFMEMORY;
    }
    HRESULT hr = provider->QueryInterface(riid, ppv);
    provider->Release();
    return hr;
}

WinLockProvider::WinLockProvider()
    : _state(std::make_shared<UnlockState>())
{
    DllAddRef();
}

WinLockProvider::~WinLockProvider()
{
    _state->StopPipeServer();
    _state->SetEvents(nullptr, 0);
    ReleaseCredentials();
    DllRelease();
}

void WinLockProvider::ReleaseCredentials()
{
    for (auto* credential : _credentials)
    {
        credential->Release();
    }
    _credentials.clear();
}

IFACEMETHODIMP WinLockProvider::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICredentialProvider))
    {
        *ppv = static_cast<ICredentialProvider*>(this);
    }
    else if (riid == __uuidof(ICredentialProviderSetUserArray))
    {
        *ppv = static_cast<ICredentialProviderSetUserArray*>(this);
    }
    else
    {
        WinLockLogGuid(L"Provider::QueryInterface: not supported", riid);
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    AddRef();
    return S_OK;
}

IFACEMETHODIMP_(ULONG) WinLockProvider::AddRef()
{
    return InterlockedIncrement(&_refCount);
}

IFACEMETHODIMP_(ULONG) WinLockProvider::Release()
{
    LONG count = InterlockedDecrement(&_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

IFACEMETHODIMP WinLockProvider::SetUsageScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario, DWORD flags)
{
    WinLockLog(L"Provider::SetUsageScenario cpus=%d flags=0x%X (unlock=%d)", static_cast<int>(scenario), flags,
        static_cast<int>(CPUS_UNLOCK_WORKSTATION));
    // Unlock only. Initial sign-in, CredUI prompts, and password changes are
    // left entirely to Windows' own providers.
    if (scenario != CPUS_UNLOCK_WORKSTATION)
    {
        return E_NOTIMPL;
    }
    _scenario = scenario;
    _state->StartPipeServer();
    return S_OK;
}

IFACEMETHODIMP WinLockProvider::SetSerialization(const CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION*)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP WinLockProvider::Advise(ICredentialProviderEvents* events, UINT_PTR adviseContext)
{
    WinLockLog(L"Provider::Advise events=%p", static_cast<void*>(events));
    _state->SetEvents(events, adviseContext);
    return S_OK;
}

IFACEMETHODIMP WinLockProvider::UnAdvise()
{
    WinLockLog(L"Provider::UnAdvise");
    _state->SetEvents(nullptr, 0);
    return S_OK;
}

IFACEMETHODIMP WinLockProvider::GetFieldDescriptorCount(DWORD* count)
{
    if (!count)
    {
        return E_INVALIDARG;
    }
    *count = FI_NUM_FIELDS;
    return S_OK;
}

IFACEMETHODIMP WinLockProvider::GetFieldDescriptorAt(DWORD index, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR** descriptor)
{
    if (!descriptor)
    {
        return E_INVALIDARG;
    }
    *descriptor = nullptr;

    CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR field;
    if (!GetWinLockFieldDescriptor(index, field))
    {
        return E_INVALIDARG;
    }
    HRESULT hr = FieldDescriptorCoAllocCopy(field, descriptor);
    WinLockLog(L"Provider::GetFieldDescriptorAt %lu hr=0x%08X", index, hr);
    return hr;
}

IFACEMETHODIMP WinLockProvider::GetCredentialCount(DWORD* count, DWORD* defaultIndex, BOOL* autoLogonWithDefault)
{
    if (!count || !defaultIndex || !autoLogonWithDefault)
    {
        return E_INVALIDARG;
    }

    *count = static_cast<DWORD>(_credentials.size());
    *defaultIndex = CREDENTIAL_PROVIDER_NO_DEFAULT;
    *autoLogonWithDefault = FALSE;

    // When an iPhone request is waiting, make that user's credential the
    // default and ask LogonUI to submit it immediately.
    for (DWORD i = 0; i < _credentials.size(); ++i)
    {
        if (_state->HasPendingFor(_credentials[i]->Sid()))
        {
            *defaultIndex = i;
            *autoLogonWithDefault = TRUE;
            break;
        }
    }
    WinLockLog(L"Provider::GetCredentialCount count=%lu default=%ld autoLogon=%d",
        *count, static_cast<long>(*defaultIndex), *autoLogonWithDefault);
    return S_OK;
}

IFACEMETHODIMP WinLockProvider::GetCredentialAt(DWORD index, ICredentialProviderCredential** credential)
{
    if (!credential)
    {
        return E_INVALIDARG;
    }
    *credential = nullptr;
    if (index >= _credentials.size())
    {
        return E_INVALIDARG;
    }
    return _credentials[index]->QueryInterface(__uuidof(ICredentialProviderCredential),
        reinterpret_cast<void**>(credential));
}

IFACEMETHODIMP WinLockProvider::SetUserArray(ICredentialProviderUserArray* users)
{
    ReleaseCredentials();
    if (!users)
    {
        _state->SetUsers({});
        return S_OK;
    }

    DWORD userCount = 0;
    HRESULT hr = users->GetCount(&userCount);
    WinLockLog(L"Provider::SetUserArray GetCount hr=0x%08X count=%lu", hr, userCount);
    if (FAILED(hr))
    {
        return hr;
    }

    std::vector<LockedUser> lockedUsers;
    try
    {
        for (DWORD i = 0; i < userCount; ++i)
        {
            ICredentialProviderUser* user = nullptr;
            if (FAILED(users->GetAt(i, &user)) || !user)
            {
                continue;
            }

            LockedUser entry;
            PWSTR sid = nullptr;
            PWSTR qualifiedName = nullptr;
            GUID providerId = GUID_NULL;
            HRESULT sidHr = user->GetSid(&sid);
            HRESULT nameHr = SUCCEEDED(sidHr)
                ? user->GetStringValue(PKEY_Identity_QualifiedUserName, &qualifiedName)
                : E_FAIL;
            WinLockLog(L"Provider::SetUserArray user %lu sid=%s sidHr=0x%08X nameHr=0x%08X",
                i, sid ? sid : L"(null)", sidHr, nameHr);
            if (SUCCEEDED(sidHr) && SUCCEEDED(nameHr) && sid && qualifiedName)
            {
                entry.sid = sid;
                entry.qualifiedName = qualifiedName;
                entry.isLocal = SUCCEEDED(user->GetProviderID(&providerId)) &&
                    IsEqualGUID(providerId, kLocalUserProvider);
                lockedUsers.push_back(entry);
            }
            CoTaskMemFree(sid);
            CoTaskMemFree(qualifiedName);
            user->Release();
        }

        for (const auto& entry : lockedUsers)
        {
            WinLockCredential* credential = nullptr;
            if (SUCCEEDED(WinLockCredential::Create(_scenario, entry, _state, &credential)))
            {
                _credentials.push_back(credential);
            }
        }
    }
    catch (...)
    {
        ReleaseCredentials();
        return E_OUTOFMEMORY;
    }

    WinLockLog(L"Provider::SetUserArray created %u credential(s)", static_cast<unsigned>(_credentials.size()));
    _state->SetUsers(std::move(lockedUsers));
    return S_OK;
}
