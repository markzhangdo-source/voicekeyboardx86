namespace VoiceKeyboard.Models;

public class AppSettings
{
    public bool IsPushToTalk { get; set; } = false;
    // HotkeyModifiers: comma-separated: Ctrl, Shift, Alt, Win
    public string HotkeyModifiers { get; set; } = "Ctrl,Shift";
    // HotkeyKey: key name for toggle mode (e.g. Space, F5, R)
    public string HotkeyKey { get; set; } = "Space";
    // WhisperModel: tiny, base, small, medium
    public string WhisperModel { get; set; } = "base";
    public string Language { get; set; } = "auto";
    public bool StartWithWindows { get; set; } = false;
    public bool ShowOverlay { get; set; } = true;
    // OverlayPosition: 0=Near cursor, 1=Top-right, 2=Bottom-right, 3=Bottom-left
    public int OverlayPosition { get; set; } = 0;
    // Auto-stop recording after N seconds of silence (0 = disabled)
    public float SilenceThreshold { get; set; } = 0f;
    // When true: paste to whichever window/cursor is active at the END of transcription.
    // When false: re-focus the window that was active when recording STARTED.
    public bool PasteToCurrentCursor { get; set; } = true;
    // Empty string = system default microphone
    public string MicrophoneDevice { get; set; } = "";
}
