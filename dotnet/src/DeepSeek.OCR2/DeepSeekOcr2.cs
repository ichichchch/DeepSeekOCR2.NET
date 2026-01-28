using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

public static class DeepSeekOcr2
{
    public static Task<DeepSeekOcr2Session> CreateSessionAsync(
        DeepSeekOcr2LocalServerOptions? serverOptions = null,
        CancellationToken cancellationToken = default)
    {
        return DeepSeekOcr2Session.CreateAsync(serverOptions, cancellationToken);
    }

    public static async Task<DeepSeekOcr2Response> RecognizeFileAsync(
        string imagePath,
        string prompt = "<image>\nFree OCR.",
        DeepSeekOcr2LocalServerOptions? serverOptions = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = await CreateSessionAsync(serverOptions, cancellationToken).ConfigureAwait(false);
        var request = DeepSeekOcr2Request.FromFile(imagePath) with { Prompt = prompt };
        return await session.Client.RecognizeAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

