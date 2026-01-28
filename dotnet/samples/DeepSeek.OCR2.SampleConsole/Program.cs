using DeepSeek.OCR2;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: DeepSeek.OCR2.SampleConsole <imagePath> [prompt]");
    return 2;
}

var imagePath = args[0];
var prompt = args.Length >= 2 ? args[1] : "<image>\nFree OCR.";

var result = await DeepSeekOcr2.RecognizeFileAsync(imagePath, prompt);
Console.WriteLine(result.Text);

return 0;
