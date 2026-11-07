#include "helpers.h"

HRESULT FieldDescriptorCoAllocCopy(
    const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR& source,
    CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR** destination)
{
    *destination = nullptr;
    auto* copy = static_cast<CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR*>(
        CoTaskMemAlloc(sizeof(CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR)));
    if (!copy)
    {
        return E_OUTOFMEMORY;
    }

    copy->dwFieldID = source.dwFieldID;
    copy->cpft = source.cpft;
    copy->guidFieldType = source.guidFieldType;
    copy->pszLabel = nullptr;

    HRESULT hr = S_OK;
    if (source.pszLabel)
    {
        hr = SHStrDupW(source.pszLabel, &copy->pszLabel);
    }

    if (SUCCEEDED(hr))
    {
        *destination = copy;
    }
    else
    {
        CoTaskMemFree(copy);
    }
    return hr;
}

static HRESULT UnicodeStringInitWithString(PWSTR value, UNICODE_STRING* target)
{
    if (!value)
    {
        return E_INVALIDARG;
    }

    size_t length = 0;
    HRESULT hr = StringCchLengthW(value, USHRT_MAX / sizeof(WCHAR), &length);
    if (FAILED(hr))
    {
        return hr;
    }

    target->Length = static_cast<USHORT>(length * sizeof(WCHAR));
    target->MaximumLength = target->Length;
    target->Buffer = value;
    return S_OK;
}

HRESULT KerbInteractiveUnlockLogonInit(
    PWSTR domain,
    PWSTR userName,
    PWSTR password,
    CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    KERB_INTERACTIVE_UNLOCK_LOGON* logon)
{
    KERB_INTERACTIVE_UNLOCK_LOGON result;
    ZeroMemory(&result, sizeof(result));
    KERB_INTERACTIVE_LOGON* interactive = &result.Logon;

    HRESULT hr = UnicodeStringInitWithString(domain, &interactive->LogonDomainName);
    if (SUCCEEDED(hr))
    {
        hr = UnicodeStringInitWithString(userName, &interactive->UserName);
    }
    if (SUCCEEDED(hr))
    {
        hr = UnicodeStringInitWithString(password, &interactive->Password);
    }
    if (FAILED(hr))
    {
        return hr;
    }

    switch (scenario)
    {
    case CPUS_UNLOCK_WORKSTATION:
        interactive->MessageType = KerbWorkstationUnlockLogon;
        break;
    case CPUS_LOGON:
        interactive->MessageType = KerbInteractiveLogon;
        break;
    default:
        return E_UNEXPECTED;
    }

    *logon = result;
    return S_OK;
}

static void PackString(const UNICODE_STRING& source, BYTE*& cursor, BYTE* base, UNICODE_STRING* target)
{
    target->Length = source.Length;
    target->MaximumLength = source.Length;
    CopyMemory(cursor, source.Buffer, source.Length);
    // LSA expects the Buffer field to hold an offset from the start of the blob.
    target->Buffer = reinterpret_cast<PWSTR>(cursor - base);
    cursor += source.Length;
}

HRESULT KerbInteractiveUnlockLogonPack(
    const KERB_INTERACTIVE_UNLOCK_LOGON& logon,
    BYTE** buffer,
    DWORD* bufferSize)
{
    *buffer = nullptr;
    *bufferSize = 0;

    const KERB_INTERACTIVE_LOGON& in = logon.Logon;
    DWORD size = sizeof(KERB_INTERACTIVE_UNLOCK_LOGON) +
        in.LogonDomainName.Length + in.UserName.Length + in.Password.Length;

    auto* out = static_cast<KERB_INTERACTIVE_UNLOCK_LOGON*>(CoTaskMemAlloc(size));
    if (!out)
    {
        return E_OUTOFMEMORY;
    }
    ZeroMemory(out, size);

    BYTE* base = reinterpret_cast<BYTE*>(out);
    BYTE* cursor = base + sizeof(KERB_INTERACTIVE_UNLOCK_LOGON);

    out->Logon.MessageType = in.MessageType;
    PackString(in.LogonDomainName, cursor, base, &out->Logon.LogonDomainName);
    PackString(in.UserName, cursor, base, &out->Logon.UserName);
    PackString(in.Password, cursor, base, &out->Logon.Password);

    *buffer = base;
    *bufferSize = size;
    return S_OK;
}

HRESULT RetrieveNegotiateAuthPackage(ULONG* authPackage)
{
    HANDLE lsa = nullptr;
    NTSTATUS status = LsaConnectUntrusted(&lsa);
    if (!NT_SUCCESS(status))
    {
        return HRESULT_FROM_NT(status);
    }

    char name[] = "Negotiate"; // NEGOSSP_NAME_A
    LSA_STRING packageName;
    packageName.Buffer = name;
    packageName.Length = static_cast<USHORT>(strlen(name));
    packageName.MaximumLength = packageName.Length + 1;

    ULONG package = 0;
    status = LsaLookupAuthenticationPackage(lsa, &packageName, &package);
    LsaDeregisterLogonProcess(lsa);

    if (!NT_SUCCESS(status))
    {
        return HRESULT_FROM_NT(status);
    }

    *authPackage = package;
    return S_OK;
}

HRESULT ProtectIfNecessaryAndCopyPassword(
    PCWSTR password,
    CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    PWSTR* protectedPassword)
{
    *protectedPassword = nullptr;
    if (!password)
    {
        return E_INVALIDARG;
    }

    PWSTR copy = nullptr;
    HRESULT hr = SHStrDupW(password, &copy);
    if (FAILED(hr))
    {
        return hr;
    }

    // Empty passwords cannot be protected; pass them through unchanged.
    if (*copy == L'\0' || scenario == CPUS_CREDUI)
    {
        *protectedPassword = copy;
        return S_OK;
    }

    CRED_PROTECTION_TYPE type;
    if (CredIsProtectedW(copy, &type) && type != CredUnprotected)
    {
        *protectedPassword = copy;
        return S_OK;
    }

    DWORD length = static_cast<DWORD>(wcslen(copy)) + 1;
    DWORD needed = 0;
    CredProtectW(FALSE, copy, length, nullptr, &needed, nullptr);
    if (GetLastError() != ERROR_INSUFFICIENT_BUFFER || needed == 0)
    {
        hr = HRESULT_FROM_WIN32(GetLastError());
        CoTaskMemFreeSecure(copy);
        return FAILED(hr) ? hr : E_FAIL;
    }

    auto* buffer = static_cast<PWSTR>(CoTaskMemAlloc(needed * sizeof(WCHAR)));
    if (!buffer)
    {
        CoTaskMemFreeSecure(copy);
        return E_OUTOFMEMORY;
    }

    if (!CredProtectW(FALSE, copy, length, buffer, &needed, nullptr))
    {
        hr = HRESULT_FROM_WIN32(GetLastError());
        CoTaskMemFreeSecure(copy);
        CoTaskMemFree(buffer);
        return hr;
    }

    CoTaskMemFreeSecure(copy);
    *protectedPassword = buffer;
    return S_OK;
}

void CoTaskMemFreeSecure(PWSTR value)
{
    if (value)
    {
        SecureZeroMemory(value, wcslen(value) * sizeof(WCHAR));
        CoTaskMemFree(value);
    }
}
