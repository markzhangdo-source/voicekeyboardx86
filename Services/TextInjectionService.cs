using System.IO;
using System.Runtime.InteropServices;

namespace VoiceKeyboard.Services;

public class TextInjectionService
{
    // ── Win32 ────────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder buf, int count);

    // ── Structs ──────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public INPUTUNION u; }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    // ── Constants ────────────────────────────────────────────────────────────

    private const uint INPUT_KEYBOARD    = 1;
    private const uint KEYEVENTF_KEYUP   = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_SHIFT   = 0x10;
    private const ushort VK_MENU    = 0x12;
    private const ushort VK_V       = 0x56;
    private const uint WM_PASTE     = 0x0302;

    // ── State ────────────────────────────────────────────────────────────────

    private IntPtr _savedWindow = IntPtr.Zero;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceKeyboard", "inject.log");

    // ── Public API ───────────────────────────────────────────────────────────

    public void SaveForegroundWindow()
    {
        _savedWindow = GetForegroundWindow();
        GetWindowThreadProcessId(_savedWindow, out uint pid);
        Log($"SaveForegroundWindow → 0x{_savedWindow:X} \"{GetTitle(_savedWindow)}\" pid={pid}  ours={Environment.ProcessId}");
    }

    /// <summary>
    /// Sets text on clipboard then pastes it.
    ///
    /// pasteToCurrentCursor=true  → paste to whichever app has focus when transcription ENDS.
    ///                               The paste runs on a background thread 300 ms later so it
    ///                               fires completely outside the hotkey-dispatch context
    ///                               (which is what was blocking SendInput before).
    ///
    /// pasteToCurrentCursor=false → re-focus the window that was active when recording STARTED.
    ///
    /// Text is LEFT in the clipboard so the user can re-paste with Ctrl+V.
    /// Must be called from the WPF UI (STA) thread (clipboard access).
    /// </summary>
    public void InjectText(string text, bool pasteToCurrentCursor)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Set clipboard on the STA thread — stays set when background thread fires
        try { System.Windows.Clipboard.SetText(text); }
        catch
        {
            Log("Clipboard.SetText failed — falling back to Unicode SendInput");
            SchedulePaste(() => InjectViaUnicode(text), 300);
            return;
        }

        if (pasteToCurrentCursor)
        {
            // Capture foreground window NOW (on UI thread) so we know the target
            IntPtr targetNow = GetForegroundWindow();
            GetWindowThreadProcessId(targetNow, out uint targetPid);

            // If our own process is somehow in front, use the saved window instead
            if (targetPid == (uint)Environment.ProcessId)
            {
                Log($"InjectText: foreground is our PID — using saved window 0x{_savedWindow:X}");
                targetNow = _savedWindow;
            }

            Log($"InjectText (pasteToCurrentCursor): scheduling paste to 0x{targetNow:X} \"{GetTitle(targetNow)}\" in 300 ms");

            IntPtr capturedTarget = targetNow;
            SchedulePaste(() => DoPasteToWindow(capturedTarget), 300);
        }
        else
        {
            // Paste to the window that was focused when recording started
            IntPtr target = _savedWindow != IntPtr.Zero ? _savedWindow : GetForegroundWindow();
            Log($"InjectText (savedWindow): scheduling paste to 0x{target:X} \"{GetTitle(target)}\" in 300 ms");

            SchedulePaste(() => DoPasteToWindow(target), 300);
        }
    }

    // ── Core paste logic (runs on background thread) ─────────────────────────

    private static void DoPasteToWindow(IntPtr target)
    {
        if (target == IntPtr.Zero)
        {
            Log("DoPasteToWindow: no target — aborting");
            return;
        }

        Log($"DoPasteToWindow → 0x{target:X} \"{GetTitle(target)}\"");

        uint targetThread = GetWindowThreadProcessId(target, out _);
        uint ourThread    = GetCurrentThreadId();

        // Attach input so we can bring the window to front + GetFocus on its thread
        bool attached = AttachThreadInput(ourThread, targetThread, true);
        BringWindowToTop(target);
        bool setFg = SetForegroundWindow(target);
        Thread.Sleep(150); // wait for focus to settle

        // Get the specifically focused child control (e.g. the edit box inside a window)
        IntPtr focusedCtrl = GetFocus();
        IntPtr pasteTarget = focusedCtrl != IntPtr.Zero ? focusedCtrl : target;

        Log($"  attached={attached}  setFg={setFg}  focusedCtrl=0x{focusedCtrl:X}  pasteTarget=0x{pasteTarget:X}");

        // 1. WM_PASTE — works for native Win32 edit/richedit controls
        bool wmpOk = PostMessage(pasteTarget, WM_PASTE, IntPtr.Zero, IntPtr.Zero);
        Log($"  PostMessage(WM_PASTE)={wmpOk} err={Marshal.GetLastWin32Error()}");

        // 2. Ctrl+V via SendInput — works for browsers, Electron, WPF, etc.
        //    Release any stray modifier keys first, then send clean Ctrl+V
        SendInput(3, [
            MakeVkInput(VK_CONTROL, true),
            MakeVkInput(VK_SHIFT,   true),
            MakeVkInput(VK_MENU,    true),
        ], Marshal.SizeOf<INPUT>());

        uint sent = SendInput(4, [
            MakeVkInput(VK_CONTROL, false),
            MakeVkInput(VK_V,       false),
            MakeVkInput(VK_V,       true),
            MakeVkInput(VK_CONTROL, true),
        ], Marshal.SizeOf<INPUT>());

        Log($"  SendInput(Ctrl+V)={sent}/4  err={Marshal.GetLastWin32Error()}");

        if (attached)
            AttachThreadInput(ourThread, targetThread, false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Runs action on a dedicated STA-free background thread after delayMs.</summary>
    private static void SchedulePaste(Action action, int delayMs)
    {
        Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            try { action(); }
            catch (Exception ex) { Log($"SchedulePaste error: {ex.Message}"); }
        });
    }

    private static void InjectViaUnicode(string text)
    {
        var inputs = new List<INPUT>();
        foreach (char c in text)
        {
            inputs.Add(MakeUnicodeInput(c, false));
            inputs.Add(MakeUnicodeInput(c, true));
        }
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeVkInput(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = keyUp ? KEYEVENTF_KEYUP : 0 } }
    };

    private static INPUT MakeUnicodeInput(char c, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0) } }
    };

    private static string GetTitle(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(hWnd, sb, 256);
        return sb.ToString();
    }

    private static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }
}
