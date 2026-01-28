using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeepSeek.OCR2;

sealed class Program
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post)
                throw new InvalidOperationException($"Expected POST, got {request.Method}.");

            if (request.RequestUri is null || !request.RequestUri.AbsolutePath.EndsWith("/ocr", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Expected /ocr, got {request.RequestUri}.");

            if (request.Content is null)
                throw new InvalidOperationException("Expected request content.");

            if (request.Content is not ByteArrayContent)
                throw new InvalidOperationException($"Expected ByteArrayContent, got {request.Content.GetType().FullName}.");

            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var json = Encoding.UTF8.GetString(bytes);

            if (!json.Contains("\"image_base64\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Missing image_base64 in JSON payload.");

            if (!json.Contains("\"prompt\"", StringComparison.Ordinal) || !json.Contains("Free OCR.", StringComparison.Ordinal))
                throw new InvalidOperationException("Missing prompt in JSON payload.");

            if (!json.Contains("AAEC", StringComparison.Ordinal))
                throw new InvalidOperationException("Missing expected base64 encoding for [0,1,2].");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"text\":\"ok\",\"elapsed_ms\":1,\"output_dir\":\"x\",\"files\":[\"a\"]}",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    public static async Task Main()
    {
        using var httpClient = new HttpClient(new CaptureHandler())
        {
            BaseAddress = new Uri("http://localhost/"),
        };

        var client = new DeepSeekOcr2Client(httpClient);

        var request = new DeepSeekOcr2Request
        {
            ImageBytes = new byte[] { 0, 1, 2 },
            Prompt = "<image>\nFree OCR.",
        };

        var result = await client.RecognizeAsync(request).ConfigureAwait(false);

        if (result.Text != "ok")
            throw new InvalidOperationException($"Unexpected result: {result.Text}");

        Console.WriteLine("PASS");
    }
}
