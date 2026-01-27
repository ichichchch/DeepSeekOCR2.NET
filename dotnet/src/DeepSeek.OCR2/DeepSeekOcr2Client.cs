using System;
using System.Net.Http;
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
            ImageBase64 = Convert.ToBase64String(request.ImageBytes),
            Prompt = request.Prompt,
            OutputDir = request.OutputDirectory,
            BaseSize = request.BaseSize,
            ImageSize = request.ImageSize,
            CropMode = request.CropMode,
            SaveResults = request.SaveResults,
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("ocr", content, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"DeepSeek OCR2 server returned {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {responseText}");

        var dto = JsonSerializer.Deserialize<OcrResponseDto>(responseText, JsonOptions)
                  ?? throw new InvalidOperationException("Failed to deserialize server response.");

        return new DeepSeekOcr2Response
        {
            Text = dto.Text ?? string.Empty,
            ElapsedMilliseconds = dto.ElapsedMs,
            OutputDirectory = dto.OutputDir,
            OutputFiles = dto.Files ?? Array.Empty<string>(),
        };
    }

    private sealed class OcrRequestDto
    {
        [JsonPropertyName("image_base64")]
        public string ImageBase64 { get; init; } = string.Empty;
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
