using Microsoft.Extensions.Logging;
using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Native.Wrappers;

namespace WinAudioRouter.Core.Audio.Services;

public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly ILogger<HotkeyService> _logger;
    private readonly IGlobalHotkeyManager _hotkeyManager;
    private readonly Dictionary<int, Action> _callbacks = [];
    private readonly Dictionary<int, (uint Modifiers, uint Key)> _hotkeyInfo = [];
    private int _nextId = 1;
    private bool _disposed;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    public HotkeyService(ILogger<HotkeyService> logger, IGlobalHotkeyManager hotkeyManager)
    {
        _logger = logger;
        _hotkeyManager = hotkeyManager;
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
    }

    public int RegisterHotkey(uint modifiers, uint key, Action callback)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HotkeyService));

        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        int id = Interlocked.Increment(ref _nextId);

        if (!_hotkeyManager.RegisterHotkey(id, modifiers, key))
        {
            _logger.LogWarning("Failed to register hotkey with modifiers {Modifiers} and key {Key}", modifiers, key);
            return -1;
        }

        _callbacks[id] = callback;
        _hotkeyInfo[id] = (modifiers, key);

        _logger.LogInformation("Registered hotkey {Id} with modifiers {Modifiers} and key {Key}", id, modifiers, key);

        return id;
    }

    public bool UnregisterHotkey(int id)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HotkeyService));

        if (!_callbacks.ContainsKey(id))
        {
            _logger.LogWarning("Hotkey {Id} not found for unregistration", id);
            return false;
        }

        if (!_hotkeyManager.UnregisterHotkey(id))
        {
            _logger.LogWarning("Failed to unregister hotkey {Id}", id);
            return false;
        }

        _callbacks.Remove(id);
        _hotkeyInfo.Remove(id);

        _logger.LogInformation("Unregistered hotkey {Id}", id);

        return true;
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (_callbacks.TryGetValue(e.Id, out var callback))
        {
            var info = _hotkeyInfo.GetValueOrDefault(e.Id);

            _logger.LogDebug("Hotkey {Id} pressed", e.Id);

            HotkeyPressed?.Invoke(this, new HotkeyEventArgs
            {
                HotkeyId = e.Id,
                Modifiers = info.Modifiers,
                Key = info.Key
            });

            callback.Invoke();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;

        if (_hotkeyManager is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _callbacks.Clear();
        _hotkeyInfo.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
