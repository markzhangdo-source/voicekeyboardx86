using System.IO;
using System.Windows;

namespace VoiceKeyboard;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private static bool _mutexOwned;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceKeyboard", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handlers
        DispatcherUnhandledException += (_, ex) =>
        {
            Log("DispatcherUnhandledException", ex.Exception);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Log("UnhandledException", ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) =>
            Log("UnobservedTaskException", ex.Exception);

        Log("Startup", null);

        // Single-instance guard.
        // Use WaitOne(2000) so a quick restart after normal exit succeeds —
        // the previous process may still be winding down when we arrive here.
        try
        {
            _instanceMutex = new Mutex(false, "VoiceKeyboard_SingleInstance");
            _mutexOwned = _instanceMutex.WaitOne(2000);
        }
        catch (AbandonedMutexException)
        {
            // Previous instance crashed — we now own the mutex
            _mutexOwned = true;
        }

        if (!_mutexOwned)
        {
            // Still running after 2 s — a real second instance
            Log("AlreadyRunning", null);
            MessageBox.Show("VoiceKeyboard is already running. Check the system tray.",
                "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log("OnStartup-window", ex);
            MessageBox.Show($"Failed to start:\n\n{ex.Message}", "VoiceKeyboard Error");
            Shutdown(1);
        }

        base.OnStartup(e);
    }

    /// <summary>
    /// Called by MainWindow.ExitApp() BEFORE Shutdown() so the mutex is freed
    /// immediately — a restart launched right after Exit won't be blocked.
    /// </summary>
    public static void ReleaseInstanceMutex()
    {
        if (_mutexOwned)
        {
            try { _instanceMutex?.ReleaseMutex(); } catch { }
            _mutexOwned = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReleaseInstanceMutex();   // no-op if already released by ExitApp
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void Log(string context, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex?.ToString() ?? "OK"}\n\n");
        }
        catch { }
    }
}
