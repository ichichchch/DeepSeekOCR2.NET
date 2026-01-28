using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

internal static class PythonPipBootstrapper
{
    public static async Task EnsurePipAsync(string pythonExe, string workingDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pythonExe))
            throw new ArgumentException("Python executable path is required.", nameof(pythonExe));
        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException("Working directory is required.", nameof(workingDirectory));

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeepSeek.OCR2",
            "bootstrap");
        Directory.CreateDirectory(cacheDir);

        var getPipPath = Path.Combine(cacheDir, "get-pip.py");
        if (!File.Exists(getPipPath))
            await DownloadAsync(new Uri("https://bootstrap.pypa.io/get-pip.py"), getPipPath, cancellationToken).ConfigureAwait(false);

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        ProcessUtil.AddArguments(psi, new[] { getPipPath, "--disable-pip-version-check", "--no-warn-script-location" });

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process: {pythonExe}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await ProcessUtil.WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"Failed to bootstrap pip. ExitCode={process.ExitCode}. Output: {output}");
        }
    }

    private static async Task DownloadAsync(Uri url, string targetPath, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

#if NET6_0_OR_GREATER
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(file, 81920, cancellationToken).ConfigureAwait(false);
    }
}
