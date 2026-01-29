using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

public sealed class DeepSeekOcr2Client
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public DeepSeekOcr2Client(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<DeepSeekOcr2Response> RecognizeAsync(DeepSeekOcr2Request request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (request.ImageBytes is null || request.ImageBytes.Length == 0)
            throw new ArgumentException("Image bytes are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("Prompt is required.", nameof(request));

        var payload = new OcrRequestDto
        {
            ImageBytes = request.ImageBytes,
            Prompt = request.Prompt,
            OutputDir = request.OutputDirectory,
            BaseSize = request.BaseSize,
            ImageSize = request.ImageSize,
            CropMode = request.CropMode,
            SaveResults = request.SaveResults,
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        using var content = new ByteArrayContent(jsonBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("ocr", content, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "DeepSeek OCR2 request timed out. If this is the first call, the Python backend may be downloading/loading the model. Increase HttpClient.Timeout (e.g. DeepSeekOcr2LocalServerOptions.OcrRequestTimeout) or pass a longer timeout via your own HttpClient.",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
#if NET6_0_OR_GREATER
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                if (responseText.IndexOf("Torch not compiled with CUDA enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new HttpRequestException(
                        $"DeepSeek OCR2 server returned {(int)response.StatusCode} ({response.ReasonPhrase}). " +
                        "The Python backend reports a CPU-only torch build while attempting to use CUDA. " +
                        "Fix: set serverOptions.Device=\"cpu\"; or install a CUDA-enabled torch and set serverOptions.TorchInstallPreset=Cuda118 (or TorchInstallPreset=None if you manage your own Python). " +
                        $"Body: {responseText}");
                }

                throw new HttpRequestException($"DeepSeek OCR2 server returned {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {responseText}");
            }

#if NET6_0_OR_GREATER
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var dto = await JsonSerializer.DeserializeAsync<OcrResponseDto>(responseStream, JsonOptions, cancellationToken).ConfigureAwait(false)
                      ?? throw new InvalidOperationException("Failed to deserialize server response.");
#else
            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<OcrResponseDto>(responseStream, JsonOptions)
                      ?? throw new InvalidOperationException("Failed to deserialize server response.");
#endif

            return new DeepSeekOcr2Response
            {
                Text = dto.Text ?? string.Empty,
                ElapsedMilliseconds = dto.ElapsedMs,
                OutputDirectory = dto.OutputDir,
                OutputFiles = dto.Files ?? Array.Empty<string>(),
            };
        }
    }

    private sealed class OcrRequestDto
    {
        [JsonPropertyName("image_base64")]
        public byte[] ImageBytes { get; init; } = Array.Empty<byte>();
        [JsonPropertyName("prompt")]
        public string Prompt { get; init; } = string.Empty;
        [JsonPropertyName("output_dir")]
        public string? OutputDir { get; init; }
        [JsonPropertyName("base_size")]
        public int BaseSize { get; init; }
        [JsonPropertyName("image_size")]
        public int ImageSize { get; init; }
        [JsonPropertyName("crop_mode")]
        public bool CropMode { get; init; }
        [JsonPropertyName("save_results")]
        public bool SaveResults { get; init; }
    }

    private sealed class OcrResponseDto
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
        [JsonPropertyName("elapsed_ms")]
        public int ElapsedMs { get; init; }
        [JsonPropertyName("output_dir")]
        public string? OutputDir { get; init; }
        [JsonPropertyName("files")]
        public string[]? Files { get; init; }
    }
}
