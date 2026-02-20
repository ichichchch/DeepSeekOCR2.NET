using DeepSeek.OCR2;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: DeepSeek.OCR2.SampleConsole <imagePath> [prompt]");
    return 2;
}

var imagePath = args[0];
var prompt = args.Length >= 2 ? args[1] : "<image>\nFree OCR.";

// Create session with progress output callback
await using var session = await DeepSeekOcr2.CreateSessionAsync(
    new DeepSeekOcr2LocalServerOptions
    {
        OcrRequestTimeout = TimeSpan.FromMinutes(30),
        BootstrapDownloadTimeout = TimeSpan.FromMinutes(30),
        // Receive Python stdout/stderr output in real-time (includes tqdm progress bars)
        OutputDataReceived = (data, isError) =>
        {
            // Write to stderr to avoid mixing with OCR result
            Console.Error.WriteLine(data);
        },
    });

var request = DeepSeekOcr2Request.FromFile(imagePath) with { Prompt = prompt };
var result = await session.Client.RecognizeAsync(request);
Console.WriteLine(result.Text);

return 0;
