using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.DnnSuperres;

namespace Artsy.Upscaler.Services;

public class UpscaleEngine : IDisposable
{
    readonly ILogger _logger;
    readonly string _modelsDir;
    DnnSuperResImpl? _superRes;
    bool _disposed;

    const string ModelUrl = "https://github.com/fannymonori/TF-LapSRN/raw/refs/heads/master/export/LapSRN_x2.pb";
    const string ModelFile = "LapSRN_x2.pb";

    public UpscaleEngine(ILogger logger, string modelsDir)
    {
        _logger = logger;
        _modelsDir = modelsDir;
    }

    public void EnsureModel()
    {
        var dir = Path.GetFullPath(_modelsDir);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var modelPath = Path.Combine(dir, ModelFile);
        if (File.Exists(modelPath))
        {
            _logger.LogInformation("Model already exists at {Path}", modelPath);
            return;
        }

        _logger.LogInformation("Downloading LapSRN model from {Url}", ModelUrl);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        var bytes = client.GetByteArrayAsync(ModelUrl).GetAwaiter().GetResult();
        File.WriteAllBytes(modelPath, bytes);

        _logger.LogInformation("Model saved to {Path} ({Bytes} bytes)", modelPath, bytes.Length);
    }

    public void LoadModel()
    {
        var modelPath = Path.Combine(Path.GetFullPath(_modelsDir), ModelFile);
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Model file not found: {modelPath}");

        _superRes = new DnnSuperResImpl("lapsrn", 2);
        _superRes.ReadModel(modelPath);
        _logger.LogInformation("LapSRN model loaded with scale factor 2");
    }

    public byte[] Upscale(byte[] inputBytes)
    {
        if (_superRes == null)
            throw new InvalidOperationException("Model not loaded. Call LoadModel() first.");

        using var inputMat = Cv2.ImDecode(inputBytes, ImreadModes.Color);
        if (inputMat.Empty())
            throw new ArgumentException("Failed to decode input image.");

        _logger.LogInformation("Decoded input image: {Width}x{Height}", inputMat.Width, inputMat.Height);

        using var outputMat = new Mat();
        _superRes.Upsample(inputMat, outputMat);

        if (outputMat.Empty())
            throw new InvalidOperationException("Upscaling produced an empty image.");

        _logger.LogInformation("Upscaled to {Width}x{Height}", outputMat.Width, outputMat.Height);

        Cv2.ImEncode(".png", outputMat, out var resultBytes);
        _logger.LogInformation("Encoded output to PNG: {Bytes} bytes", resultBytes.Length);

        return resultBytes;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _superRes?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
