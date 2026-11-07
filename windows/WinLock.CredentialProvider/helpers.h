// Serialization helpers, adapted from the patterns in Microsoft's
// Windows-classic-samples (CredentialProviders/SampleV2CredentialProvider).
#pragma once

#include "common.h"

// Copies a field descriptor into CoTaskMem memory (caller/LogonUI frees it).
HRESULT FieldDescriptorCoAllocCopy(
    const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR& source,
    CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR** destination);

// Builds a KERB_INTERACTIVE_UNLOCK_LOGON that points at the given strings
// (no copies; the strings must outlive the call to the Pack function).
HRESULT KerbInteractiveUnlockLogonInit(
    PWSTR domain,
    PWSTR userName,
    PWSTR password,
    CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    KERB_INTERACTIVE_UNLOCK_LOGON* logon);

// Packs the logon structure into a single CoTaskMem buffer with relative
// string offsets, which is the format LogonUI/LSA expect.
HRESULT KerbInteractiveUnlockLogonPack(
    const KERB_INTERACTIVE_UNLOCK_LOGON& logon,
    BYTE** buffer,
    DWORD* bufferSize);

// Looks up the "Negotiate" authentication package id.
HRESULT RetrieveNegotiateAuthPackage(ULONG* authPackage);

// Returns a CoTaskMem copy of the password, CredProtect-ed when appropriate
// (as Windows' own password provider does). Caller zeroes and frees it.
HRESULT ProtectIfNecessaryAndCopyPassword(
    PCWSTR password,
    CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    PWSTR* protectedPassword);

// Zeroes and frees a CoTaskMem string.
void CoTaskMemFreeSecure(PWSTR value);
