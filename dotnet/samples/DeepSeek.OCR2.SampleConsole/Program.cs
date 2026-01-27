using DeepSeek.OCR2;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: DeepSeek.OCR2.SampleConsole <imagePath> [prompt]");
    return 2;
}

var imagePath = args[0];
var prompt = args.Length >= 2 ? args[1] : "<image>\nFree OCR.";

await using var server = await DeepSeekOcr2LocalServer.StartAsync(new DeepSeekOcr2LocalServerOptions());
using var http = new HttpClient { BaseAddress = server.BaseUri };
var client = new DeepSeekOcr2Client(http);

var request = DeepSeekOcr2Request.FromFile(imagePath) with
{
    Prompt = prompt,
    SaveResults = true,
};

var result = await client.RecognizeAsync(request);
Console.WriteLine(result.Text);

return 0;
