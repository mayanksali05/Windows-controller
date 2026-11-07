// COM plumbing for the WinLock credential provider DLL.
#include <initguid.h>
#include "Provider.h"
#include "log.h"

HINSTANCE g_hinst = nullptr;
static LONG g_dllRefCount = 0;

void DllAddRef()
{
    InterlockedIncrement(&g_dllRefCount);
}

void DllRelease()
{
    InterlockedDecrement(&g_dllRefCount);
}

class WinLockClassFactory final : public IClassFactory
{
public:
    WinLockClassFactory() { DllAddRef(); }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv)
        {
            return E_POINTER;
        }
        if (riid == __uuidof(IUnknown) || riid == __uuidof(IClassFactory))
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&_refCount); }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        LONG count = InterlockedDecrement(&_refCount);
        if (count == 0)
        {
            delete this;
        }
        return count;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (!ppv)
        {
            return E_POINTER;
        }
        *ppv = nullptr;
        if (outer)
        {
            return CLASS_E_NOAGGREGATION;
        }
        HRESULT hr = WinLockProvider::Create(riid, ppv);
        WinLockLog(L"ClassFactory::CreateInstance hr=0x%08X", hr);
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock)
        {
            DllAddRef();
        }
        else
        {
            DllRelease();
        }
        return S_OK;
    }

private:
    virtual ~WinLockClassFactory() { DllRelease(); }
    LONG _refCount = 1;
};

STDAPI DllCanUnloadNow()
{
    return g_dllRefCount > 0 ? S_FALSE : S_OK;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }
    *ppv = nullptr;
    if (!IsEqualCLSID(rclsid, CLSID_WinLockProvider))
    {
        WinLockLogGuid(L"DllGetClassObject: unknown CLSID", rclsid);
        return CLASS_E_CLASSNOTAVAILABLE;
    }
    WinLockLog(L"DllGetClassObject: WinLock provider requested");

    auto* factory = new (std::nothrow) WinLockClassFactory();
    if (!factory)
    {
        return E_OUTOFMEMORY;
    }
    HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

BOOL WINAPI DllMain(HINSTANCE hinstance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_hinst = hinstance;
        DisableThreadLibraryCalls(hinstance);
        wchar_t image[MAX_PATH] = L"?";
        GetModuleFileNameW(nullptr, image, ARRAYSIZE(image));
        WinLockLog(L"DLL loaded into %s", image);
    }
    return TRUE;
}
