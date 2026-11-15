// Windows system decoder for Denia's silent menu video. No bundled FFmpeg/runtime.
// Decode/readback stays on one MTA worker; Godot owns all texture work on its main thread.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mferror.h>
#include <d3d11.h>
#include <d3d10.h>
#include <wrl/client.h>
#include <new>
#include <cstring>
using Microsoft::WRL::ComPtr;
struct Decoder {
    ComPtr<IMFSourceReader> reader;
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    ComPtr<IMFDXGIDeviceManager> manager;
    UINT32 width=0, height=0;
    bool com=false, mf=false;
    ~Decoder() {
        reader.Reset(); manager.Reset(); context.Reset(); device.Reset();
        if(mf) MFShutdown();
        if(com) CoUninitialize();
    }
};
#define CHECK(x) do { HRESULT hr=(x); if(FAILED(hr)) return hr; } while(0)
static HRESULT Open(Decoder* d, const wchar_t* path) {
    CHECK(CoInitializeEx(nullptr, COINIT_MULTITHREADED)); d->com=true;
    CHECK(MFStartup(MF_VERSION)); d->mf=true;
    D3D_FEATURE_LEVEL level;
    CHECK(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,
        D3D11_CREATE_DEVICE_VIDEO_SUPPORT|D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        nullptr,0,D3D11_SDK_VERSION,&d->device,&level,&d->context));
    ComPtr<ID3D10Multithread> protect;
    CHECK(d->context.As(&protect)); protect->SetMultithreadProtected(TRUE);
    UINT token=0; CHECK(MFCreateDXGIDeviceManager(&token,&d->manager));
    CHECK(d->manager->ResetDevice(d->device.Get(),token));
    ComPtr<IMFAttributes> attrs; CHECK(MFCreateAttributes(&attrs,4));
    CHECK(attrs->SetUnknown(MF_SOURCE_READER_D3D_MANAGER,d->manager.Get()));
    CHECK(attrs->SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS,TRUE));
    CHECK(attrs->SetUINT32(MF_SOURCE_READER_DISABLE_DXVA,FALSE));
    CHECK(MFCreateSourceReaderFromURL(path,attrs.Get(),&d->reader));
    CHECK(d->reader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS,FALSE));
    CHECK(d->reader->SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM,TRUE));
    ComPtr<IMFMediaType> requested; CHECK(MFCreateMediaType(&requested));
    CHECK(requested->SetGUID(MF_MT_MAJOR_TYPE,MFMediaType_Video));
    CHECK(requested->SetGUID(MF_MT_SUBTYPE,MFVideoFormat_NV12));
    CHECK(d->reader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM,nullptr,requested.Get()));
    ComPtr<IMFMediaType> actual; CHECK(d->reader->GetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM,&actual));
    CHECK(MFGetAttributeSize(actual.Get(),MF_MT_FRAME_SIZE,&d->width,&d->height));
    return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __cdecl denia_video_open(const wchar_t* path, void** output, int* width, int* height) {
    *output=nullptr;
    auto d=new(std::nothrow) Decoder(); if(!d) return E_OUTOFMEMORY;
    HRESULT hr=Open(d,path);
    if(FAILED(hr)) { delete d; return hr; }
    *width=d->width; *height=d->height; *output=d; return S_OK;
}
// S_FALSE means EOS. The caller seeks only after delivering all queued frames.
extern "C" __declspec(dllexport) HRESULT __cdecl denia_video_read(void* handle, unsigned char* luma, int luma_capacity, unsigned char* chroma, int chroma_capacity, long long* pts, int* hardware) {
    auto d=static_cast<Decoder*>(handle);
    if(!d || luma_capacity < int(d->width*d->height) || chroma_capacity < int(d->width*d->height/2)) return E_INVALIDARG;
    for(;;) {
        DWORD flags=0, stream=0; ComPtr<IMFSample> sample;
        CHECK(d->reader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM,0,&stream,&flags,pts,&sample));
        if(flags & MF_SOURCE_READERF_ENDOFSTREAM) return S_FALSE;
        if(flags & MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED) {
            ComPtr<IMFMediaType> actual; UINT32 width=0,height=0; GUID subtype;
            CHECK(d->reader->GetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM,&actual));
            CHECK(MFGetAttributeSize(actual.Get(),MF_MT_FRAME_SIZE,&width,&height));
            CHECK(actual->GetGUID(MF_MT_SUBTYPE,&subtype));
            if(width!=d->width || height!=d->height || subtype!=MFVideoFormat_NV12) return MF_E_INVALIDMEDIATYPE;
        }
        if(!sample) continue;
        ComPtr<IMFMediaBuffer> buffer; CHECK(sample->GetBufferByIndex(0,&buffer));
        ComPtr<IMFDXGIBuffer> surface; *hardware=SUCCEEDED(buffer.As(&surface)) ? 1 : 0;
        ComPtr<IMF2DBuffer> image;
        if(SUCCEEDED(buffer.As(&image))) {
            BYTE* data=nullptr; LONG pitch=0; CHECK(image->Lock2D(&data,&pitch));
            if(pitch < LONG(d->width)) { image->Unlock2D(); return MF_E_INVALIDMEDIATYPE; }
            for(UINT y=0;y<d->height;++y) memcpy(luma+y*d->width,data+y*pitch,d->width);
            BYTE* uv=data+d->height*pitch;
            for(UINT y=0;y<d->height/2;++y) memcpy(chroma+y*d->width,uv+y*pitch,d->width);
            return image->Unlock2D();
        }
        BYTE* data=nullptr; DWORD maximum=0,length=0; CHECK(buffer->Lock(&data,&maximum,&length));
        UINT needed=d->width*d->height*3/2;
        HRESULT result=length>=needed ? S_OK : MF_E_BUFFERTOOSMALL;
        if(SUCCEEDED(result)) { memcpy(luma,data,d->width*d->height); memcpy(chroma,data+d->width*d->height,d->width*d->height/2); }
        buffer->Unlock(); return result;
    }
}
extern "C" __declspec(dllexport) HRESULT __cdecl denia_video_rewind(void* handle) {
    auto d=static_cast<Decoder*>(handle); if(!d) return E_INVALIDARG;
    PROPVARIANT pos; PropVariantInit(&pos); pos.vt=VT_I8; pos.hVal.QuadPart=0;
    return d->reader->SetCurrentPosition(GUID_NULL,pos);
}
extern "C" __declspec(dllexport) void __cdecl denia_video_close(void* handle) { delete static_cast<Decoder*>(handle); }
