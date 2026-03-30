using System.Windows;
using System.Windows.Interop;
using VoiceKeyboard.Services;
using VoiceKeyboard.Windows;
using System.Drawing;
using System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;
using DrawingColor = System.Drawing.Color;

namespace VoiceKeyboard;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AudioCaptureService _audioService;
    private readonly TranscriptionService _transcriptionService;
    private readonly TextInjectionService _textInjection;
    private readonly GlobalHotkeyService _hotkeyService;

    private OverlayWindow? _overlay;
    private NotifyIcon? _trayIcon;
    private HwndSource? _hwndSource;

    private bool _isProcessing;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        _settingsService.Load();

        _audioService = new AudioCaptureService();
        _transcriptionService = new TranscriptionService();
        _textInjection = new TextInjectionService();
        _hotkeyService = new GlobalHotkeyService(_settingsService.Settings);

        _hotkeyService.RecordingStartRequested += OnRecordingStartRequested;
        _hotkeyService.RecordingStopRequested += OnRecordingStopRequested;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceKeyboard", "crash.log");

    private static void Log(string msg)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath)!);
            System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }
        catch { }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Log("MainWindow_Loaded start");

            // Get HWND for hotkey registration
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hwndSource?.AddHook(WndProc);
            Log("HwndSource OK");

            _hotkeyService.Initialize(new WindowInteropHelper(this).Handle);
            _hotkeyService.ApplySettings(_settingsService.Settings);
            Log("Hotkey OK");

            // Initialize model if downloaded
            _ = InitializeModelAsync();

            Log("Setting up tray icon");
            SetupTrayIcon();
            Log("Tray icon OK");

            _overlay = new OverlayWindow();
            Log("Overlay OK — fully loaded");

            // Hide the host window — tray icon is now the only UI
            Hide();
        }
        catch (Exception ex)
        {
            Log($"CRASH in Loaded: {ex}");
            MessageBox.Show($"Startup error:\n\n{ex.Message}\n\nSee log: {LogPath}", "VoiceKeyboard Error");
        }
    }

    private async Task InitializeModelAsync()
    {
        var settings = _settingsService.Settings;
        if (TranscriptionService.IsModelDownloaded(settings.WhisperModel, SettingsService.ModelsDir))
        {
            try
            {
                var modelPath = TranscriptionService.GetModelPath(settings.WhisperModel, SettingsService.ModelsDir);
                var lang = settings.Language == "auto" ? "auto" : settings.Language;
                await _transcriptionService.InitializeAsync(modelPath, lang);
            }
            catch (Exception ex)
            {
                // File may be corrupted — delete it so the status dot turns red in Settings
                TranscriptionService.DeleteModel(settings.WhisperModel, SettingsService.ModelsDir);
                ShowBalloon("VoiceKeyboard",
                    $"Model file was invalid and has been removed.\nOpen Settings to re-download it.\n({ex.Message})",
                    ToolTipIcon.Warning);
            }
        }
        else
        {
            ShowBalloon("VoiceKeyboard",
                $"No model downloaded. Right-click the tray icon \u2192 Settings to download a model.",
                ToolTipIcon.Info);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkeyService.HandleHotkeyMessage(msg, wParam))
            handled = true;
        return IntPtr.Zero;
    }

    private void OnRecordingStartRequested(object? sender, EventArgs e)
    {
        if (_isProcessing) return;

        // Save the currently focused window before we do anything
        _textInjection.SaveForegroundWindow();

        int deviceIndex = AudioCaptureService.GetDeviceIndex(_settingsService.Settings.MicrophoneDevice);
        _audioService.StartRecording(deviceIndex);

        if (_settingsService.Settings.ShowOverlay)
        {
            _overlay ??= new OverlayWindow();
            _overlay.ShowRecording();
        }
    }

    private async void OnRecordingStopRequested(object? sender, EventArgs e)
    {
        if (!_audioService.IsRecording) return;

        _isProcessing = true;

        var wavData = _audioService.StopRecording();

        if (_settingsService.Settings.ShowOverlay)
            _overlay?.ShowTranscribing();

        try
        {
            if (!_transcriptionService.IsInitialized)
            {
                var settings = _settingsService.Settings;
                if (!TranscriptionService.IsModelDownloaded(settings.WhisperModel, SettingsService.ModelsDir))
                {
                    _overlay?.Hide();
                    ShowBalloon("VoiceKeyboard",
                        "No model downloaded. Open Settings to download one.",
                        ToolTipIcon.Warning);
                    return;
                }

                var modelPath = TranscriptionService.GetModelPath(settings.WhisperModel, SettingsService.ModelsDir);
                await _transcriptionService.InitializeAsync(modelPath, settings.Language);
            }

            var text = await _transcriptionService.TranscribeAsync(wavData);

            if (!string.IsNullOrWhiteSpace(text))
                _textInjection.InjectText(text, _settingsService.Settings.PasteToCurrentCursor);

            if (_settingsService.Settings.ShowOverlay)
                _overlay?.ShowDone(text);
        }
        catch (Exception ex)
        {
            _overlay?.Hide();
            ShowBalloon("VoiceKeyboard", $"Transcription error: {ex.Message}", ToolTipIcon.Error);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "VoiceKeyboard \u2014 Local AI Voice Transcription"
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApp));

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
    }

    private void OpenSettings()
    {
        var settingsWin = new SettingsWindow(_settingsService, _transcriptionService);
        settingsWin.Owner = this;

        if (settingsWin.ShowDialog() == true)
        {
            // Reload hotkey with new settings
            _hotkeyService.ApplySettings(_settingsService.Settings);

            // Reload model if changed
            _ = InitializeModelAsync();
        }
    }

    private void ExitApp()
    {
        _hotkeyService.ForceStopRecording();

        // Release mutex immediately so a quick restart isn't blocked
        App.ReleaseInstanceMutex();

        // Hide icon before dispose — prevents ghost icon lingering in tray
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void ShowBalloon(string title, string message, ToolTipIcon icon)
    {
        _trayIcon?.ShowBalloonTip(3000, title, message, icon);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true; // Prevent closing — only exit via tray menu
    }

    private static DrawingIcon CreateTrayIcon()
    {
        // Load the icon embedded in the exe (set via <ApplicationIcon> in the csproj)
        try
        {
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(exePath))
                return DrawingIcon.ExtractAssociatedIcon(exePath) ?? FallbackIcon();
        }
        catch { }
        return FallbackIcon();
    }

    private static DrawingIcon FallbackIcon()
    {
        // High-contrast dark icon that's visible on both light and dark taskbars
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(DrawingColor.Transparent);

        // Microphone body — dark blue fill, white outline
        using var bodyBrush = new SolidBrush(DrawingColor.FromArgb(30, 100, 220));
        g.FillRectangle(bodyBrush, 5, 2, 6, 8);
        g.DrawRectangle(Pens.White, 5, 2, 6, 8);

        // Stand
        using var standPen = new System.Drawing.Pen(DrawingColor.White, 1.5f);
        g.DrawArc(standPen, 3, 6, 10, 6, 0, 180);
        g.DrawLine(standPen, 8, 12, 8, 14);
        g.DrawLine(standPen, 5, 14, 11, 14);

        return DrawingIcon.FromHandle(bmp.GetHicon());
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _hotkeyService.Dispose();
        _audioService.Dispose();
        _transcriptionService.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }
}
