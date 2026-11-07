// WinLock credential provider — shared definitions.
//
// This DLL is loaded by LogonUI (as SYSTEM) on the lock screen. It adds a
// "Unlock with iPhone" sign-in option for the locked user. The WinLock
// service forwards the user's real Windows password (kept on the iPhone,
// released by Face ID) through a local named pipe; this provider packs it into
// a standard KERB_INTERACTIVE_UNLOCK_LOGON and hands it to Windows, which
// validates it exactly as if it had been typed. Nothing is bypassed: a wrong
// password fails like a wrong password typed on the keyboard.
#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#ifndef SECURITY_WIN32
#define SECURITY_WIN32
#endif

#define WIN32_NO_STATUS
#include <windows.h>
#undef WIN32_NO_STATUS
#include <ntstatus.h>

#include <unknwn.h>
#include <credentialprovider.h>
#include <ntsecapi.h>
#include <security.h>
#include <shlwapi.h>
#include <propkey.h>
#include <sddl.h>
#include <wincred.h>
// Keep strsafe from deprecating CRT names that STL headers use.
#define STRSAFE_NO_DEPRECATE
#include <strsafe.h>

#ifndef NT_SUCCESS
#define NT_SUCCESS(Status) (((NTSTATUS)(Status)) >= 0)
#endif
// wincred.h values; older SDK headers may not define them.
#ifndef CRED_PACK_PROTECTED_CREDENTIALS
#define CRED_PACK_PROTECTED_CREDENTIALS 0x1
#endif
#ifndef CRED_PACK_ID_PROVIDER_CREDENTIALS
#define CRED_PACK_ID_PROVIDER_CREDENTIALS 0x8
#endif

// {B8C6F5E2-4A5D-4C1B-9E0F-3D2A7C8B9E41}
// CLSID of the WinLock credential provider. Must match install-credential-provider.ps1.
DEFINE_GUID(CLSID_WinLockProvider,
    0xb8c6f5e2, 0x4a5d, 0x4c1b, 0x9e, 0x0f, 0x3d, 0x2a, 0x7c, 0x8b, 0x9e, 0x41);

// Well-known field-type GUIDs (shlguid.h in the Windows SDK). Declared under our
// own names so this compiles with SDKs that do or do not define them.
// CPFG_CREDENTIAL_PROVIDER_LOGO  {2D837775-F6CD-464E-A745-482FD0B47493}
DEFINE_GUID(WINLOCK_CPFG_LOGO,
    0x2d837775, 0xf6cd, 0x464e, 0xa7, 0x45, 0x48, 0x2f, 0xd0, 0xb4, 0x74, 0x93);
// CPFG_CREDENTIAL_PROVIDER_LABEL {286BBFF3-BAD4-438F-B007-79B7267C3D48}
DEFINE_GUID(WINLOCK_CPFG_LABEL,
    0x286bbff3, 0xbad4, 0x438f, 0xb0, 0x07, 0x79, 0xb7, 0x26, 0x7c, 0x3d, 0x48);

// DLL lifetime (implemented in dll.cpp).
void DllAddRef();
void DllRelease();
extern HINSTANCE g_hinst;

// Named pipe used by WinLock.Service to deliver an unlock request.
#define WINLOCK_PIPE_NAME L"\\\\.\\pipe\\WinLockUnlock"

// Wire format (little-endian), request:  "WLU1" | uint32 passwordBytes | UTF-16LE password
//                                response: uint8 code | int32 NTSTATUS
enum UnlockResultCode : BYTE
{
    UNLOCK_RESULT_SUCCESS = 0,     // Windows accepted the password; session unlocked
    UNLOCK_RESULT_REJECTED = 1,    // Windows rejected the password (see NTSTATUS)
    UNLOCK_RESULT_NO_USER = 2,     // the caller is not the locked user
    UNLOCK_RESULT_TIMEOUT = 3,     // LogonUI did not pick up the credential in time
    UNLOCK_RESULT_BAD_REQUEST = 4, // malformed request
    UNLOCK_RESULT_BUSY = 5,        // another unlock is already in progress
};

// Upper bound on the password payload (bytes of UTF-16). 256 UTF-16 chars is
// the Windows password limit.
#define WINLOCK_MAX_PASSWORD_BYTES 512
// How long an accepted request waits for LogonUI to submit and report a result.
#define WINLOCK_RESULT_TIMEOUT_MS 15000
