// Minimal diagnostic log for the credential provider.
//
// Written to %ProgramData%\WinLock\provider.log (LogonUI runs as SYSTEM, which
// can write there). Records only call flow, HRESULTs, SIDs and result codes:
// NEVER pass a password or any credential material to WinLockLog.
// The file is reset when it grows past 512 KB.
#pragma once

#include "common.h"

void WinLockLog(const wchar_t* format, ...);
void WinLockLogGuid(const wchar_t* prefix, REFGUID guid);
