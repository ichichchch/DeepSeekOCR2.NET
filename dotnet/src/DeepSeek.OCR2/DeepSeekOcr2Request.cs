using System;

namespace DeepSeek.OCR2;

public sealed record DeepSeekOcr2Request
{
    public byte[] ImageBytes { get; init; } = Array.Empty<byte>();

    public string Prompt { get; init; } = "<image>\nFree OCR.";

    public string? OutputDirectory { get; init; }

    public int BaseSize { get; init; } = 1024;

    public int ImageSize { get; init; } = 768;

    public bool CropMode { get; init; } = true;

    public bool SaveResults { get; init; } = false;

    public static DeepSeekOcr2Request FromFile(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path is required.", nameof(imagePath));

        return new DeepSeekOcr2Request
        {
            ImageBytes = System.IO.File.ReadAllBytes(imagePath),
        };
    }
}
