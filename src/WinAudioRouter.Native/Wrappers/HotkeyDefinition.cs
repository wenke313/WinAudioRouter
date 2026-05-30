namespace WinAudioRouter.Native.Wrappers;

public sealed class HotkeyDefinition
{
    public int Id { get; set; }
    public uint Modifiers { get; set; }
    public uint Key { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
}
