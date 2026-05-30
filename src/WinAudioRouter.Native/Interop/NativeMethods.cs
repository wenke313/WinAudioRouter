using System.Runtime.InteropServices;

namespace WinAudioRouter.Native.Interop;

[ComImport]
[Guid("568b9108-44bf-40b4-9006-86afee027e57")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfigVista
{
    [PreserveSig]
    int GetMixFormat([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IntPtr format);

    [PreserveSig]
    int GetDeviceFormat([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, int deviceFormat, out IntPtr format);

    [PreserveSig]
    int SetDeviceFormat([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref IntPtr format, ref IntPtr endpointFormat);

    [PreserveSig]
    int GetProcessingPeriod([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref int defaultPeriod, ref int minimumPeriod, ref int currentPeriod);

    [PreserveSig]
    int SetProcessingPeriod([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref int period);

    [PreserveSig]
    int GetShareMode([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, out int shareMode);

    [PreserveSig]
    int SetShareMode([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref int shareMode);

    [PreserveSig]
    int GetPropertyValue([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, int deviceFormat, ref PropertyKey propertyKey, out IntPtr value);

    [PreserveSig]
    int SetPropertyValue([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, int deviceFormat, ref PropertyKey propertyKey, ref IntPtr value);

    [PreserveSig]
    int SetDefaultEndpoint([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);

    [PreserveSig]
    int SetEndpointVisibility([In, MarshalAs(UnmanagedType.LPWStr)] string deviceId, int visible);
}

[ComImport]
[Guid("870af920-5c01-4e7e-9c90-749463e23c2e")]
internal class PolicyConfigClient
{
}

internal struct PropertyKey
{
    public Guid FormatId;
    public int PropertyId;
}

internal static partial class NativeMethods
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public const int WM_HOTKEY = 0x0312;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
