using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VoiceKeyboard.Models;
using VoiceKeyboard.Services;

namespace VoiceKeyboard.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly TranscriptionService _transcriptionService;
    private AppSettings _editSettings;
    private CancellationTokenSource? _downloadCts;

    // Hotkey capture state
    private bool _isCapturingHotkey;
    private string _capturedModifiers = "Ctrl,Shift";
    private string _capturedKey = "Space";

    public SettingsWindow(SettingsService settingsService, TranscriptionService transcriptionService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _transcriptionService = transcriptionService;
        _editSettings = CloneSettings(settingsService.Settings);

        LoadSettings();
        KeyDown += SettingsWindow_KeyDown;
    }

    private void LoadSettings()
    {
        var s = _editSettings;

        RadioToggle.IsChecked = !s.IsPushToTalk;
        RadioPTT.IsChecked = s.IsPushToTalk;
        RadioToggle.Checked += Mode_Changed;
        RadioPTT.Checked += Mode_Changed;

        _capturedModifiers = s.HotkeyModifiers;
        _capturedKey = s.HotkeyKey;
        UpdateHotkeyDisplay();
        UpdatePttNote();

        SelectComboByTag(ModelCombo, s.WhisperModel);
        SelectComboByTag(LangCombo, s.Language);

        PopulateMicDevices(s.MicrophoneDevice);

        ChkPasteToCurrentCursor.IsChecked = s.PasteToCurrentCursor;
        ChkOverlay.IsChecked = s.ShowOverlay;
        ChkStartup.IsChecked = s.StartWithWindows;

        // Subscribe after controls are populated to avoid firing during InitializeComponent
        ModelCombo.SelectionChanged += ModelCombo_SelectionChanged;

        UpdateModelStatus();
    }

    private void PopulateMicDevices(string selectedName)
    {
        MicCombo.Items.Clear();
        foreach (var (_, name) in AudioCaptureService.GetAvailableDevices())
            MicCombo.Items.Add(name);

        // Select saved device, fall back to "System Default"
        int idx = 0;
        for (int i = 0; i < MicCombo.Items.Count; i++)
        {
            if (MicCombo.Items[i]?.ToString() == selectedName) { idx = i; break; }
        }
        MicCombo.SelectedIndex = idx;
    }

    private void BtnRefreshMic_Click(object sender, RoutedEventArgs e)
    {
        var current = MicCombo.SelectedItem?.ToString() ?? "";
        PopulateMicDevices(current);
    }

    private void Mode_Changed(object sender, RoutedEventArgs e) => UpdatePttNote();

    private void UpdatePttNote()
    {
        bool isPtt = RadioPTT.IsChecked == true;
        PttNote.Visibility = isPtt ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateHotkeyDisplay()
    {
        bool isPtt = RadioPTT.IsChecked == true;
        if (isPtt)
        {
            HotkeyDisplay.Text = $"{_capturedModifiers.Replace(",", " + ")}  (hold)";
        }
        else
        {
            var parts = _capturedModifiers.Split(',').Select(m => m.Trim()).ToList();
            if (!string.IsNullOrEmpty(_capturedKey))
                parts.Add(_capturedKey);
            HotkeyDisplay.Text = string.Join(" + ", parts);
        }
    }

    private void BtnChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturingHotkey)
        {
            _isCapturingHotkey = false;
            BtnChangeHotkey.Content = "Change...";
            HotkeyDisplay.Foreground = WpfBrushes.White;
            return;
        }

        _isCapturingHotkey = true;
        BtnChangeHotkey.Content = "Cancel";
        HotkeyDisplay.Text = "Press your hotkey combination...";
        HotkeyDisplay.Foreground = new SolidColorBrush(WpfColor.FromRgb(255, 220, 100));
        Focus();
    }

    private void SettingsWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isCapturingHotkey) return;

        e.Handled = true;

        var key = e.Key;
        // Ignore lone modifier presses
        if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
            key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
            key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
            key == System.Windows.Input.Key.System)
        {
            // Only modifiers so far — keep capturing
            var mods = new List<string>();
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) mods.Add("Ctrl");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) mods.Add("Shift");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0) mods.Add("Alt");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0) mods.Add("Win");
            HotkeyDisplay.Text = string.Join(" + ", mods) + " + ...";
            return;
        }

        // Got a full combination
        var modifiers = new List<string>();
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) modifiers.Add("Ctrl");
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) modifiers.Add("Shift");
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0) modifiers.Add("Alt");
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0) modifiers.Add("Win");

        if (modifiers.Count == 0)
        {
            HotkeyDisplay.Text = "Please include at least one modifier (Ctrl, Shift, Alt)";
            return;
        }

        _capturedModifiers = string.Join(",", modifiers);
        _capturedKey = key.ToString();
        _isCapturingHotkey = false;
        BtnChangeHotkey.Content = "Change...";
        HotkeyDisplay.Foreground = WpfBrushes.White;
        UpdateHotkeyDisplay();
    }

    private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateModelStatus();

    private void UpdateModelStatus()
    {
        var modelName = GetSelectedTag(ModelCombo);
        var downloaded = TranscriptionService.IsModelDownloaded(modelName, SettingsService.ModelsDir);

        ModelStatusDot.Fill = downloaded
            ? new SolidColorBrush(WpfColor.FromRgb(80, 220, 120))
            : new SolidColorBrush(WpfColor.FromRgb(255, 68, 68));

        if (downloaded)
        {
            ModelStatusText.Text = "Downloaded and ready";
            BtnDownload.IsEnabled = false;
            BtnDownload.Content = "Downloaded";
        }
        else
        {
            var sizeStr = FormatBytes(TranscriptionService.GetModelSize(modelName));
            ModelStatusText.Text = $"Not downloaded  ({sizeStr})";
            BtnDownload.IsEnabled = true;
            BtnDownload.Content = "Download";
        }
    }

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        var modelName = GetSelectedTag(ModelCombo);
        _downloadCts = new CancellationTokenSource();

        BtnDownload.IsEnabled = false;
        BtnDownload.Content = "Downloading...";
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.IsIndeterminate = true;
        ModelStatusText.Text = $"Downloading {modelName} model...";

        try
        {
            await TranscriptionService.DownloadModelAsync(
                modelName,
                SettingsService.ModelsDir,
                cancellationToken: _downloadCts.Token);

            UpdateModelStatus();
            StatusBar.Text = $"{modelName} model downloaded successfully.";
        }
        catch (OperationCanceledException)
        {
            StatusBar.Text = "Download cancelled.";
            UpdateModelStatus();
        }
        catch (Exception ex)
        {
            StatusBar.Text = $"Download failed: {ex.Message}";
            BtnDownload.IsEnabled = true;
            BtnDownload.Content = "Retry";
        }
        finally
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();

        _editSettings.IsPushToTalk = RadioPTT.IsChecked == true;
        _editSettings.HotkeyModifiers = _capturedModifiers;
        _editSettings.HotkeyKey = _capturedKey;
        _editSettings.WhisperModel = GetSelectedTag(ModelCombo);
        _editSettings.Language = GetSelectedTag(LangCombo);
        _editSettings.MicrophoneDevice = MicCombo.SelectedItem?.ToString() == "System Default"
            ? "" : (MicCombo.SelectedItem?.ToString() ?? "");
        _editSettings.PasteToCurrentCursor = ChkPasteToCurrentCursor.IsChecked == true;
        _editSettings.ShowOverlay = ChkOverlay.IsChecked == true;
        _editSettings.StartWithWindows = ChkStartup.IsChecked == true;

        _settingsService.Settings = _editSettings;
        _settingsService.Save();

        // Update startup registry
        SetStartWithWindows(_editSettings.StartWithWindows);

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
        DialogResult = false;
        Close();
    }

    private static void SetStartWithWindows(bool enable)
    {
        try
        {
            var regKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)!;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                regKey.SetValue("VoiceKeyboard", $"\"{exePath}\"");
            }
            else
            {
                regKey.DeleteValue("VoiceKeyboard", false);
            }
        }
        catch { /* Non-critical */ }
    }

    private static void SelectComboByTag(WpfComboBox combo, string tag)
    {
        foreach (WpfComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string GetSelectedTag(WpfComboBox combo)
    {
        return (combo.SelectedItem as WpfComboBoxItem)?.Tag?.ToString() ?? "base";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1} GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F0} MB";
        return $"{bytes / 1_000.0:F0} KB";
    }

    private static AppSettings CloneSettings(AppSettings src) => new()
    {
        IsPushToTalk = src.IsPushToTalk,
        HotkeyModifiers = src.HotkeyModifiers,
        HotkeyKey = src.HotkeyKey,
        WhisperModel = src.WhisperModel,
        Language = src.Language,
        PasteToCurrentCursor = src.PasteToCurrentCursor,
        ShowOverlay = src.ShowOverlay,
        StartWithWindows = src.StartWithWindows,
        OverlayPosition = src.OverlayPosition,
        SilenceThreshold = src.SilenceThreshold,
        MicrophoneDevice = src.MicrophoneDevice
    };
}
