using System;

namespace DeepSeek.OCR2;

public sealed record DeepSeekOcr2LocalServerOptions
{
    public string PythonExecutablePath { get; init; } = "python";

    public bool EnsureVenv { get; init; } = false;

    public string? VenvDirectory { get; init; }

    public DeepSeekOcr2TorchInstallPreset TorchInstallPreset { get; init; } = DeepSeekOcr2TorchInstallPreset.None;

    public string? OfflineWheelDirectory { get; init; }

    public bool PreferOfflineWheels { get; init; } = false;

    public string TorchVersion { get; init; } = "2.6.0";

    public string TorchVisionVersion { get; init; } = "0.21.0";

    public string TorchAudioVersion { get; init; } = "2.6.0";

    public string[] PipInstallArguments { get; init; } = Array.Empty<string>();

    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 0;

    public string ModelName { get; init; } = "deepseek-ai/DeepSeek-OCR-2";

    public string Device { get; init; } = "cuda";

    public string DType { get; init; } = "bfloat16";

    public string AttnImpl { get; init; } = "flash_attention_2";

    public string? WorkingDirectory { get; init; }

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(120);
}
