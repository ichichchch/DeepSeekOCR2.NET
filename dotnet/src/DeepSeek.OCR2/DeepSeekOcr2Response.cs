using System.Collections.Generic;

namespace DeepSeek.OCR2;

public sealed record DeepSeekOcr2Response
{
    public string Text { get; init; } = string.Empty;

    public int ElapsedMilliseconds { get; init; }

    public string? OutputDirectory { get; init; }

    public IReadOnlyList<string> OutputFiles { get; init; } = System.Array.Empty<string>();
}
