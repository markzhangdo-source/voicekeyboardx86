using System.Runtime.InteropServices;
using System.Windows;
using VoiceKeyboard.Models;

namespace VoiceKeyboard.Services;

public class GlobalHotkeyService : IDisposable
{
    // ── Win32 constants ──────────────────────────────────────────────────────
    public  const int WM_HOTKEY      = 0x0312;
    private const int MOD_NONE       = 0x0000;
    private const int MOD_ALT        = 0x0001;
    private const int MOD_CONTROL    = 0x0002;
    private const int MOD_SHIFT      = 0x0004;
    private const int MOD_WIN        = 0x0008;
    private const int MOD_NOREPEAT   = 0x4000;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_KEYUP       = 0x0101;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int WM_SYSKEYUP    = 0x0105;
    private const int VK_CONTROL     = 0x11;
    private const int VK_SHIFT       = 0x10;
    private const int VK_MENU        = 0x12;    // Alt
    private const int VK_LCONTROL    = 0xA2;
    private const int VK_RCONTROL    = 0xA3;
    private const int VK_LSHIFT      = 0xA0;
    private const int VK_RSHIFT      = 0xA1;
    private const int VK_LMENU       = 0xA4;
    private const int VK_RMENU       = 0xA5;
    private const int HotkeyId       = 9001;

    // ── Win32 imports ────────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    // Returns high-bit set if key is physically down
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    // ── State ────────────────────────────────────────────────────────────────
    private IntPtr   _hwnd;
    private AppSettings _settings = new();
    private IntPtr   _hookHandle  = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookCallback;   // keep alive so GC doesn't collect it
    private bool     _isRecording;
    private bool     _disposed;

    public event EventHandler? RecordingStartRequested;
    public event EventHandler? RecordingStopRequested;

    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceKeyboard", "crash.log");

    // ── Public API ───────────────────────────────────────────────────────────
    public GlobalHotkeyService(AppSettings settings) => _settings = settings;

    public void Initialize(IntPtr hwnd) => _hwnd = hwnd;

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        Unregister();

        if (settings.IsPushToTalk)
            InstallKeyboardHook();
        else
            RegisterToggleHotkey();
    }

    /// <summary>Called from WndProc in MainWindow — toggle mode only.</summary>
    public bool HandleHotkeyMessage(int message, IntPtr wParam)
    {
        if (message != WM_HOTKEY || wParam.ToInt32() != HotkeyId) return false;

        if (_isRecording)
        {
            _isRecording = false;
            RecordingStopRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _isRecording = true;
            RecordingStartRequested?.Invoke(this, EventArgs.Empty);
        }
        return true;
    }

    public void ForceStopRecording()
    {
        if (_isRecording)
        {
            _isRecording = false;
            RecordingStopRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // ── Toggle mode ──────────────────────────────────────────────────────────
    private void RegisterToggleHotkey()
    {
        if (_hwnd == IntPtr.Zero) return;

        int modifiers = MOD_NOREPEAT;
        if (_settings.HotkeyModifiers.Contains("Ctrl"))  modifiers |= MOD_CONTROL;
        if (_settings.HotkeyModifiers.Contains("Shift")) modifiers |= MOD_SHIFT;
        if (_settings.HotkeyModifiers.Contains("Alt"))   modifiers |= MOD_ALT;
        if (_settings.HotkeyModifiers.Contains("Win"))   modifiers |= MOD_WIN;

        int vkCode = GetVirtualKeyCode(_settings.HotkeyKey);
        if (vkCode == 0) return;

        bool ok = RegisterHotKey(_hwnd, HotkeyId, modifiers, vkCode);
        Log($"RegisterHotKey({_settings.HotkeyModifiers}+{_settings.HotkeyKey}) = {ok}  err={Marshal.GetLastWin32Error()}");
    }

    // ── Push-to-talk mode ────────────────────────────────────────────────────
    private void InstallKeyboardHook()
    {
        // Keep delegate in a field so the GC never collects it while the hook is active
        _hookCallback = LowLevelKeyboardProcHandler;

        // For WH_KEYBOARD_LL the hMod must be the current process module handle.
        // GetModuleHandle(null) returns the .exe module — the correct value for .NET apps.
        var hMod = GetModuleHandle(null);
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallback, hMod, 0);

        int err = Marshal.GetLastWin32Error();
        Log($"InstallKeyboardHook: handle=0x{_hookHandle:X}  hMod=0x{hMod:X}  err={err}  settings={_settings.HotkeyModifiers}");

        if (_hookHandle == IntPtr.Zero)
            Log($"WARNING: keyboard hook installation FAILED (err={err}). PTT will not work.");
    }

    private IntPtr LowLevelKeyboardProcHandler(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool keyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool keyUp   = wParam == (IntPtr)WM_KEYUP   || wParam == (IntPtr)WM_SYSKEYUP;

            // Only react to modifier key transitions — no need to fire on every keystroke
            bool isModifier = kb.vkCode is VK_CONTROL or VK_LCONTROL or VK_RCONTROL
                                       or VK_SHIFT   or VK_LSHIFT   or VK_RSHIFT
                                       or VK_MENU    or VK_LMENU    or VK_RMENU;
            if (!isModifier && !_isRecording)
            {
                // Non-modifier pressed while combo not active — skip to avoid spam
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            // WH_KEYBOARD_LL fires BEFORE the OS commits the key state, so
            // GetAsyncKeyState for the key being processed right now is stale.
            // We override it with the actual transition direction.
            bool comboActive = CheckPttCombo(kb.vkCode, keyDown);

            if (comboActive && !_isRecording)
            {
                _isRecording = true;
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    RecordingStartRequested?.Invoke(this, EventArgs.Empty));
            }
            else if (!comboActive && _isRecording)
            {
                _isRecording = false;
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    RecordingStopRequested?.Invoke(this, EventArgs.Empty));
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>
    /// Checks if the PTT modifier combo is currently active.
    /// currentVk / currentIsDown describe the key event being processed RIGHT NOW —
    /// we use these to override GetAsyncKeyState which hasn't been updated yet by the OS.
    /// </summary>
    private bool CheckPttCombo(uint currentVk, bool currentIsDown)
    {
        bool needsCtrl  = _settings.HotkeyModifiers.Contains("Ctrl");
        bool needsShift = _settings.HotkeyModifiers.Contains("Shift");
        bool needsAlt   = _settings.HotkeyModifiers.Contains("Alt");

        if (!needsCtrl && !needsShift && !needsAlt) return false;

        bool ctrlIsCurrent  = currentVk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL;
        bool shiftIsCurrent = currentVk is VK_SHIFT   or VK_LSHIFT   or VK_RSHIFT;
        bool altIsCurrent   = currentVk is VK_MENU    or VK_LMENU    or VK_RMENU;

        // For the key currently being processed, use the transition direction.
        // For all others, read live hardware state from GetAsyncKeyState.
        bool ctrlDown  = ctrlIsCurrent  ? currentIsDown : (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool shiftDown = shiftIsCurrent ? currentIsDown : (GetAsyncKeyState(VK_SHIFT)   & 0x8000) != 0;
        bool altDown   = altIsCurrent   ? currentIsDown : (GetAsyncKeyState(VK_MENU)    & 0x8000) != 0;

        return (!needsCtrl || ctrlDown) && (!needsShift || shiftDown) && (!needsAlt || altDown);
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────
    private void Unregister()
    {
        if (_hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, HotkeyId);

        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        _hookCallback = null;
        _isRecording  = false;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    public static int GetVirtualKeyCode(string keyName) =>
        keyName.ToUpperInvariant() switch
        {
            "SPACE" or " "              => 0x20,
            "RETURN" or "ENTER"         => 0x0D,
            "BACK" or "BACKSPACE"       => 0x08,
            "TAB"                       => 0x09,
            "ESCAPE" or "ESC"           => 0x1B,
            "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
            "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
            "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
            "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
            "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
            "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59,
            "Z" => 0x5A,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            _ => 0
        };

    private static void Log(string msg)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath)!);
            System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed) { Unregister(); _disposed = true; }
    }
}
