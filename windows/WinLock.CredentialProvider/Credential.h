// One "Unlock with iPhone" sign-in option, attached to a locked user's tile.
#pragma once

#include "UnlockState.h"

enum WinLockFieldId : DWORD
{
    FI_TILEIMAGE = 0,
    FI_LABEL,
    FI_LARGE_TEXT,
    FI_STATUS,
    FI_SUBMIT,
    FI_NUM_FIELDS,
};

// Fills a static descriptor for the given field (label strings are static).
bool GetWinLockFieldDescriptor(DWORD fieldId, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR& descriptor);

class WinLockCredential : public ICredentialProviderCredential2
{
public:
    static HRESULT Create(
        CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
        const LockedUser& user,
        std::shared_ptr<UnlockState> state,
        WinLockCredential** credential);

    const std::wstring& Sid() const { return _user.sid; }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // ICredentialProviderCredential
    IFACEMETHODIMP Advise(ICredentialProviderCredentialEvents* events) override;
    IFACEMETHODIMP UnAdvise() override;
    IFACEMETHODIMP SetSelected(BOOL* autoLogon) override;
    IFACEMETHODIMP SetDeselected() override;
    IFACEMETHODIMP GetFieldState(
        DWORD fieldId,
        CREDENTIAL_PROVIDER_FIELD_STATE* state,
        CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE* interactiveState) override;
    IFACEMETHODIMP GetStringValue(DWORD fieldId, LPWSTR* value) override;
    IFACEMETHODIMP GetBitmapValue(DWORD fieldId, HBITMAP* bitmap) override;
    IFACEMETHODIMP GetCheckboxValue(DWORD fieldId, BOOL* checked, LPWSTR* label) override;
    IFACEMETHODIMP GetSubmitButtonValue(DWORD fieldId, DWORD* adjacentTo) override;
    IFACEMETHODIMP GetComboBoxValueCount(DWORD fieldId, DWORD* count, DWORD* selected) override;
    IFACEMETHODIMP GetComboBoxValueAt(DWORD fieldId, DWORD item, LPWSTR* value) override;
    IFACEMETHODIMP SetStringValue(DWORD fieldId, LPCWSTR value) override;
    IFACEMETHODIMP SetCheckboxValue(DWORD fieldId, BOOL checked) override;
    IFACEMETHODIMP SetComboBoxSelectedValue(DWORD fieldId, DWORD selected) override;
    IFACEMETHODIMP CommandLinkClicked(DWORD fieldId) override;
    IFACEMETHODIMP GetSerialization(
        CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE* response,
        CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* serialization,
        LPWSTR* optionalStatusText,
        CREDENTIAL_PROVIDER_STATUS_ICON* optionalStatusIcon) override;
    IFACEMETHODIMP ReportResult(
        NTSTATUS status,
        NTSTATUS subStatus,
        LPWSTR* optionalStatusText,
        CREDENTIAL_PROVIDER_STATUS_ICON* optionalStatusIcon) override;

    // ICredentialProviderCredential2
    IFACEMETHODIMP GetUserSid(LPWSTR* sid) override;

private:
    WinLockCredential(
        CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
        LockedUser user,
        std::shared_ptr<UnlockState> state);
    virtual ~WinLockCredential();

    void SplitQualifiedName(std::wstring& domain, std::wstring& userName) const;

    LONG _refCount = 1;
    CREDENTIAL_PROVIDER_USAGE_SCENARIO _scenario;
    LockedUser _user;
    std::shared_ptr<UnlockState> _state;
    ICredentialProviderCredentialEvents* _events = nullptr;
};
