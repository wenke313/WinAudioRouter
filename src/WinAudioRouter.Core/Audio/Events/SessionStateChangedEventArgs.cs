using WinAudioRouter.Core.Audio.Models;

namespace WinAudioRouter.Core.Audio.Events;

public class SessionStateChangedEventArgs : EventArgs
{
    public string SessionId { get; init; } = string.Empty;
    public AudioSessionState OldState { get; init; }
    public AudioSessionState NewState { get; init; }
}
