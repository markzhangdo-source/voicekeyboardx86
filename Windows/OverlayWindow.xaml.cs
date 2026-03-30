using System.Windows;
using System.Windows.Media;

namespace VoiceKeyboard.Windows;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void ShowRecording()
    {
        StatusText.Text = "Recording...";
        StatusDot.Fill = new SolidColorBrush(WpfColor.FromRgb(255, 68, 68));
        PositionNearCursor();
        Show();
    }

    public void ShowTranscribing()
    {
        StatusText.Text = "Transcribing...";
        StatusDot.Fill = new SolidColorBrush(WpfColor.FromRgb(100, 180, 255));
    }

    public void ShowDone(string text)
    {
        StatusText.Text = string.IsNullOrEmpty(text) ? "No speech detected" : "Done \u2713";
        StatusDot.Fill = new SolidColorBrush(WpfColor.FromRgb(80, 220, 120));
        // Auto-hide after 1.5s
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        timer.Tick += (_, _) => { timer.Stop(); Hide(); };
        timer.Start();
    }

    private void PositionNearCursor()
    {
        var pos = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(pos);
        var dpi = VisualTreeHelper.GetDpi(this);

        double x = (pos.X + 20) / dpi.DpiScaleX;
        double y = (pos.Y + 20) / dpi.DpiScaleY;

        // Keep on-screen
        double screenRight = screen.WorkingArea.Right / dpi.DpiScaleX;
        double screenBottom = screen.WorkingArea.Bottom / dpi.DpiScaleY;

        if (x + Width > screenRight) x = screenRight - Width - 10;
        if (y + Height > screenBottom) y = screenBottom - Height - 10;

        Left = x;
        Top = y;
    }
}
