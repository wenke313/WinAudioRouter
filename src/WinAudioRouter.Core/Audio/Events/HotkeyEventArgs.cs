namespace WinAudioRouter.Core.Audio.Events;

public class HotkeyEventArgs : EventArgs
{
    public int HotkeyId { get; init; }
    public uint Modifiers { get; init; }
    public uint Key { get; init; }
}
