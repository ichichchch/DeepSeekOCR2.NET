using DeepSeek.OCR2;

var templatePath = Path.Combine(AppContext.BaseDirectory, "DeepSeek.OCR2", "templates", "deepseek-ocr2.defaults.json");
Console.WriteLine($"Template exists: {File.Exists(templatePath)}");
Console.WriteLine($"Template path: {templatePath}");

var bundledRoot = Path.Combine(AppContext.BaseDirectory, "DeepSeek.OCR2", "bundled");
Console.WriteLine($"Bundled root exists: {Directory.Exists(bundledRoot)}");
Console.WriteLine($"Bundled root path: {bundledRoot}");

var type = typeof(DeepSeekOcr2LocalServer).Assembly.GetType("DeepSeek.OCR2.DeepSeekOcr2BundledAssets", throwOnError: false);
if (type is not null)
{
    var modelDir = type.GetMethod("TryGetBundledModelDirectory")?.Invoke(null, null) as string;
    var wheelsDir = type.GetMethod("TryGetBundledWheelsDirectory")?.Invoke(null, null) as string;
    var pythonExe = type.GetMethod("TryGetBundledPythonExecutable")?.Invoke(null, null) as string;
    Console.WriteLine($"Bundled model dir: {modelDir}");
    Console.WriteLine($"Bundled wheels dir: {wheelsDir}");
    Console.WriteLine($"Bundled python exe: {pythonExe}");
}

var run = string.Equals(Environment.GetEnvironmentVariable("DEEPSEEK_OCR2_RUN"), "1", StringComparison.Ordinal);
if (!run)
{
    Console.WriteLine("Set DEEPSEEK_OCR2_RUN=1 and provide <imagePath> [prompt] to run the integration smoke test.");
    return 0;
}

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: DeepSeek.OCR2.NuGetSmokeTest <imagePath> [prompt]");
    return 2;
}

var response = await DeepSeekOcr2.RecognizeFileAsync(
    imagePath: args[0],
    prompt: args.Length > 1 ? args[1] : "<image>\nFree OCR.",
    serverOptions: new DeepSeekOcr2LocalServerOptions
    {
        Device = "cpu",
        OcrRequestTimeout = TimeSpan.FromMinutes(30),
        BootstrapDownloadTimeout = TimeSpan.FromMinutes(30),
    });

Console.WriteLine(response.Text);
return 0;
