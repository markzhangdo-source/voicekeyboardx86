using System.IO;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceKeyboard.Services;

public class TranscriptionService : IDisposable
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private string _currentModelPath = string.Empty;
    private string _currentLanguage = "auto";
    private bool _disposed;

    public bool IsInitialized => _factory != null && _processor != null;

    public event EventHandler<string>? StatusChanged;

    public async Task InitializeAsync(string modelPath, string language = "auto")
    {
        if (_currentModelPath == modelPath && _currentLanguage == language && IsInitialized)
            return;

        DisposeProcessor();

        StatusChanged?.Invoke(this, "Loading model...");
        _factory = WhisperFactory.FromPath(modelPath);

        var builder = _factory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount)
            .WithSingleSegment();

        if (language != "auto")
            builder = builder.WithLanguage(language);

        _processor = builder.Build();
        _currentModelPath = modelPath;
        _currentLanguage = language;
        StatusChanged?.Invoke(this, "Model ready");

        await Task.CompletedTask;
    }

    public async Task<string> TranscribeAsync(byte[] wavData, CancellationToken token = default)
    {
        if (_processor == null)
            throw new InvalidOperationException("Transcription service not initialized. Load a model first.");

        using var stream = new MemoryStream(wavData);
        var result = new StringBuilder();

        await foreach (var segment in _processor.ProcessAsync(stream, token))
        {
            result.Append(segment.Text);
        }

        return result.ToString().Trim();
    }

    public static async Task DownloadModelAsync(
        string modelName,
        string targetDir,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modelType = modelName.ToLower() switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            _ => GgmlType.Base
        };

        Directory.CreateDirectory(targetDir);
        var targetPath = GetModelPath(modelName, targetDir);

        // Write to a temp file first — if download fails/cancels the real path
        // stays clean and won't be mistaken for a valid model on next startup.
        var tempPath = targetPath + ".tmp";
        try
        {
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(
                modelType, QuantizationType.NoQuantization, cancellationToken);
            using var fileStream = File.Create(tempPath);
            await modelStream.CopyToAsync(fileStream, cancellationToken);
        }
        catch
        {
            // Clean up partial temp file, then re-throw so caller can handle
            try { File.Delete(tempPath); } catch { }
            throw;
        }

        // Atomic-ish rename: replace any stale model file with the fresh download
        if (File.Exists(targetPath)) File.Delete(targetPath);
        File.Move(tempPath, targetPath);
    }

    public static string GetModelPath(string modelName, string modelsDir) =>
        Path.Combine(modelsDir, $"ggml-{modelName}.bin");

    /// <summary>
    /// Returns true only if the model file exists AND is large enough to be a valid download
    /// (at least 30 % of the expected size — guards against partial/corrupted downloads).
    /// </summary>
    public static bool IsModelDownloaded(string modelName, string modelsDir)
    {
        var path = GetModelPath(modelName, modelsDir);
        if (!File.Exists(path)) return false;
        var minBytes = GetModelSize(modelName) * 3 / 10;   // 30 % threshold
        return new FileInfo(path).Length >= minBytes;
    }

    /// <summary>Deletes the model file so the user can re-download a clean copy.</summary>
    public static void DeleteModel(string modelName, string modelsDir)
    {
        var path = GetModelPath(modelName, modelsDir);
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public static long GetModelSize(string modelName) => modelName.ToLower() switch
    {
        "tiny" => 75_000_000L,
        "base" => 142_000_000L,
        "small" => 466_000_000L,
        "medium" => 1_500_000_000L,
        _ => 142_000_000L
    };

    private void DisposeProcessor()
    {
        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeProcessor();
            _disposed = true;
        }
    }
}
