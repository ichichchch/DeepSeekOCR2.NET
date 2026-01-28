using System;

namespace DeepSeek.OCR2;

public sealed record DeepSeekOcr2LocalServerOptions
{
    public string PythonExecutablePath { get; init; } = "";

    public bool AutoSetupPython { get; init; } = true;

    public string PythonRuntimeVersion { get; init; } = "3.10.11";

    public string? PythonRuntimeDirectory { get; init; }

    public bool EnsureVenv { get; init; } = true;

    public string? VenvDirectory { get; init; }

    public DeepSeekOcr2TorchInstallPreset TorchInstallPreset { get; init; } = DeepSeekOcr2TorchInstallPreset.Cpu;

    public string? OfflineWheelDirectory { get; init; }

    public bool PreferOfflineWheels { get; init; } = false;

    public string TorchVersion { get; init; } = "2.6.0";

    public string TorchVisionVersion { get; init; } = "0.21.0";

    public string TorchAudioVersion { get; init; } = "2.6.0";

    public string[] PipInstallArguments { get; init; } = Array.Empty<string>();

    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 0;

    public string ModelName { get; init; } = "deepseek-ai/DeepSeek-OCR-2";

    public string Device { get; init; } = "cpu";

    public string DType { get; init; } = "float32";

    public string AttnImpl { get; init; } = "sdpa";

    public string? WorkingDirectory { get; init; }

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(120);
}
