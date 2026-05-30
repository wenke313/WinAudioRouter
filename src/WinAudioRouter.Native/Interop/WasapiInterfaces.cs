using System.Runtime.InteropServices;

namespace WinAudioRouter.Native.Interop;

internal static class WasapiGuids
{
    public static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    public static readonly Guid IID_IAudioRenderClient = new("F294ACFC-3146-4483-A7BF-ADDCA7B26084");
    public static readonly Guid IID_IAudioClock = new("CD63314F-3FBA-4A1B-812C-EF963E6A2F0E");
    public static readonly Guid IID_IAudioStreamVolume = new("93014887-24F4-41A2-9147-C61CBE7A3C70");
    public static readonly Guid IID_IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
}

internal static class AudClnt
{
    public const int SHAREMODE_SHARED = 0;
    public const int SHAREMODE_EXCLUSIVE = 1;

    public const int STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    public const int STREAMFLAGS_NOPERSIST = 0x00080000;
    public const int STREAMFLAGS_LOOPBACK = 0x00100000;
    public const int STREAMFLAGS_AUTOCONVERTPCM = unchecked((int)0x80000000);
    public const int STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;

    public const int BUFFERFLAGS_DATA_DISCONTINUITY = 0x1;
    public const int BUFFERFLAGS_SILENT = 0x2;
    public const int BUFFERFLAGS_TIMESTAMP_ERROR = 0x4;

    public const int E_NOT_INITIALIZED = unchecked((int)0x88890001);
    public const int E_ALREADY_INITIALIZED = unchecked((int)0x88890002);
    public const int E_WRONG_ENDPOINT_TYPE = unchecked((int)0x88890003);
    public const int E_DEVICE_INVALIDATED = unchecked((int)0x88890004);
    public const int E_BUFFER_SIZE_NOT_ALIGNED = unchecked((int)0x8889000D);
    public const int E_BUFFER_SIZE_ERROR = unchecked((int)0x88890016);
    public const int E_CPUUSAGE_EXCEEDED = unchecked((int)0x88890019);
    public const int E_RESOURCES_INVALIDATED = unchecked((int)0x88890026);

    public const long HNS_PER_MS = 10_000;
    public const long HNS_PER_SECOND = 10_000_000;
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr device);

    [PreserveSig]
    int GetDevice([In, MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int dwClsCtx, IntPtr activationParams, out IntPtr interfacePtr);

    [PreserveSig]
    int OpenPropertyStore(int stgmAccess, out IntPtr properties);

    [PreserveSig]
    int GetId([Out, MarshalAs(UnmanagedType.LPWStr)] out string id);

    [PreserveSig]
    int GetState(out int state);
}

[ComImport]
[Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig]
    int GetMixFormat(out IntPtr formatPtr);

    [PreserveSig]
    int GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);

    [PreserveSig]
    int Initialize(
        int shareMode,
        int streamFlags,
        long hnsBufferDuration,
        long hnsPeriodicity,
        IntPtr format,
        ref Guid audioSessionGuid);

    [PreserveSig]
    int GetBufferSize(out uint numBufferFrames);

    [PreserveSig]
    int GetCurrentPadding(out int numPaddingFrames);

    [PreserveSig]
    int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatchFormat);

    [PreserveSig]
    int GetService(ref Guid interfaceId, out IntPtr servicePtr);

    [PreserveSig]
    int Start();

    [PreserveSig]
    int Stop();

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int SetEventHandle(IntPtr eventHandle);
}

[ComImport]
[Guid("F294ACFC-3146-4483-A7BF-ADDCA7B26084")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioRenderClient
{
    [PreserveSig]
    int GetBuffer(uint numFramesRequested, out IntPtr dataBufferPtr);

    [PreserveSig]
    int ReleaseBuffer(uint numFramesWritten, int flags);
}

[ComImport]
[Guid("93014887-24F4-41A2-9147-C61CBE7A3C70")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioStreamVolume
{
    [PreserveSig]
    int GetChannelCount(out uint channelCount);

    [PreserveSig]
    int SetChannelVolume(uint channelIndex, float level);

    [PreserveSig]
    int GetChannelVolume(uint channelIndex, out float level);

    [PreserveSig]
    int SetAllVolumes(uint channelCount, [In] float[] levels);

    [PreserveSig]
    int GetAllVolumes(uint channelCount, [Out] float[] levels);
}

[StructLayout(LayoutKind.Sequential)]
internal struct WAVEFORMATEX
{
    public ushort wFormatTag;
    public ushort nChannels;
    public uint nSamplesPerSec;
    public uint nAvgBytesPerSec;
    public ushort nBlockAlign;
    public ushort wBitsPerSample;
    public ushort cbSize;
}
