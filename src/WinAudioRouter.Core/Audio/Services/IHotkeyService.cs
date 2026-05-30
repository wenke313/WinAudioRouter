using WinAudioRouter.Core.Audio.Events;

namespace WinAudioRouter.Core.Audio.Services;

public interface IHotkeyService
{
    int RegisterHotkey(uint modifiers, uint key, Action callback);
    bool UnregisterHotkey(int id);
    event EventHandler<HotkeyEventArgs>? HotkeyPressed;
}
