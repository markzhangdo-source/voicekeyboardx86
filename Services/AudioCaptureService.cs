using System.IO;
using NAudio.Wave;

namespace VoiceKeyboard.Services;

public class AudioCaptureService : IDisposable
{
    // Whisper expects 16 kHz, 16-bit, mono
    private static readonly WaveFormat WhisperFormat = new WaveFormat(16000, 16, 1);

    private WaveInEvent? _waveIn;
    private MemoryStream? _audioStream;
    private WaveFileWriter? _waveWriter;
    private bool _isRecording;

    public bool IsRecording => _isRecording;

    public event EventHandler<float>? AudioLevelChanged;

    // Returns (deviceIndex, productName) for every available input device.
    // Index -1 is the system default (always first in the list).
    public static IReadOnlyList<(int Index, string Name)> GetAvailableDevices()
    {
        var list = new List<(int, string)> { (-1, "System Default") };
        for (int i = 0; i < WaveIn.DeviceCount; i++)
            list.Add((i, WaveIn.GetCapabilities(i).ProductName));
        return list;
    }

    // Returns -1 (system default) if name is empty or not found.
    public static int GetDeviceIndex(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return -1;
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            if (WaveIn.GetCapabilities(i).ProductName == deviceName)
                return i;
        }
        return -1; // not found → fall back to default
    }

    public void StartRecording(int deviceNumber = -1)
    {
        if (_isRecording) return;

        _audioStream = new MemoryStream();
        _waveIn = new WaveInEvent
        {
            WaveFormat = WhisperFormat,
            BufferMilliseconds = 100,
            DeviceNumber = deviceNumber < 0 ? 0 : deviceNumber   // NAudio uses 0 for default
        };
        _waveWriter = new WaveFileWriter(_audioStream, WhisperFormat);

        _waveIn.DataAvailable += OnDataAvailable;
        _isRecording = true;
        _waveIn.StartRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_waveWriter == null || !_isRecording) return;
        _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);

        // RMS level for the overlay meter
        float sum = 0;
        for (int i = 0; i < e.BytesRecorded - 1; i += 2)
        {
            short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
            float norm = sample / 32768f;
            sum += norm * norm;
        }
        AudioLevelChanged?.Invoke(this, (float)Math.Sqrt(sum / (e.BytesRecorded / 2)));
    }

    public byte[] StopRecording()
    {
        if (!_isRecording) return [];

        _isRecording = false;
        _waveIn?.StopRecording();
        _waveWriter?.Flush();

        var data = _audioStream?.ToArray() ?? [];

        _waveIn?.Dispose();
        _waveWriter?.Dispose();
        _audioStream?.Dispose();
        _waveIn = null;
        _waveWriter = null;
        _audioStream = null;

        return data;
    }

    public void Dispose()
    {
        if (_isRecording) StopRecording();
        _waveIn?.Dispose();
        _waveWriter?.Dispose();
        _audioStream?.Dispose();
    }
}
