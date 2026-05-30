using System.Runtime.InteropServices;
using WinAudioRouter.Native.Interop;

namespace WinAudioRouter.Native.Wrappers;

public class HotkeyPressedEventArgs : EventArgs
{
    public int Id { get; }

    public HotkeyPressedEventArgs(int id)
    {
        Id = id;
    }
}

public interface IGlobalHotkeyManager
{
    bool RegisterHotkey(int id, uint modifiers, uint key);
    bool UnregisterHotkey(int id);
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
}

public sealed class GlobalHotkeyManager : IGlobalHotkeyManager, IDisposable
{
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly IntPtr _hwnd;
    private readonly WndProcDelegate _wndProc;
    private readonly HashSet<int> _registeredIds = [];
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public GlobalHotkeyManager()
    {
        _wndProc = WndProc;

        string className = $"WinAudioRouter_Hotkey_{Guid.NewGuid():N}";
        IntPtr hInstance = GetModuleHandle(IntPtr.Zero);

        IntPtr classNamePtr = Marshal.StringToHGlobalUni(className);
        try
        {
            var wc = new WNDCLASSW
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = hInstance,
                lpszClassName = classNamePtr
            };

            ushort atom = RegisterClassW(ref wc);
            if (atom == 0)
                throw new InvalidOperationException($"Failed to register window class. Error: {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }

        _hwnd = CreateWindowExW(0, className, string.Empty, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to create message-only window. Error: {Marshal.GetLastWin32Error()}");
    }

    public bool RegisterHotkey(int id, uint modifiers, uint key)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GlobalHotkeyManager));

        if (_registeredIds.Contains(id))
            return false;

        if (!HotkeyInterop.RegisterHotKey(_hwnd, id, modifiers, key))
            return false;

        _registeredIds.Add(id);
        return true;
    }

    public bool UnregisterHotkey(int id)
    {
        if (!_registeredIds.Contains(id))
            return false;

        if (!HotkeyInterop.UnregisterHotKey(_hwnd, id))
            return false;

        _registeredIds.Remove(id);
        return true;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == HotkeyInterop.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(id));
            return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (int id in _registeredIds)
        {
            HotkeyInterop.UnregisterHotKey(_hwnd, id);
        }

        _registeredIds.Clear();

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSW
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(IntPtr lpModuleName);
}
