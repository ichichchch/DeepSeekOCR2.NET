using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

internal static class PythonRuntimeBootstrapper
{
    public static async Task<string> ResolvePythonExecutableAsync(DeepSeekOcr2LocalServerOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.PythonExecutablePath))
            return options.PythonExecutablePath;

        var bundledPython = DeepSeekOcr2BundledAssets.TryGetBundledPythonExecutable();
        if (!string.IsNullOrWhiteSpace(bundledPython))
            return bundledPython;

        foreach (var candidate in GetSystemCandidates())
        {
            if (await CanRunAsync(candidate, cancellationToken).ConfigureAwait(false))
                return candidate;
        }

        if (!options.AutoSetupPython)
            throw new InvalidOperationException("Python executable was not found. Install Python 3.10+ or set DeepSeekOcr2LocalServerOptions.PythonExecutablePath.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new InvalidOperationException("Python executable was not found. Install Python 3.10+ or set DeepSeekOcr2LocalServerOptions.PythonExecutablePath.");

        return await EnsurePortablePythonOnWindowsAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> GetSystemCandidates()
    {
        yield return "python";
        yield return "python3";
    }

    private static async Task<bool> CanRunAsync(string pythonExe, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            ProcessUtil.AddArguments(psi, new[] { "--version" });

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                return false;

            await ProcessUtil.WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> EnsurePortablePythonOnWindowsAsync(DeepSeekOcr2LocalServerOptions options, CancellationToken cancellationToken)
    {
        var version = string.IsNullOrWhiteSpace(options.PythonRuntimeVersion) ? "3.10.11" : options.PythonRuntimeVersion.Trim();
        var runtimeDir = options.PythonRuntimeDirectory;
        if (string.IsNullOrWhiteSpace(runtimeDir))
        {
            runtimeDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeepSeek.OCR2",
                "python",
                version,
                "win-x64");
        }

        Directory.CreateDirectory(runtimeDir);

        var pythonExe = Path.Combine(runtimeDir, "python.exe");
        if (!File.Exists(pythonExe))
        {
            var zipName = $"python-{version}-embed-amd64.zip";
            var zipPath = Path.Combine(runtimeDir, zipName);
            if (!File.Exists(zipPath))
            {
                var url = new Uri($"https://www.python.org/ftp/python/{version}/{zipName}");
                await DownloadAsync(url, zipPath, cancellationToken).ConfigureAwait(false);
            }

            ExtractZipOverwrite(zipPath, runtimeDir);
        }

        PatchEmbeddedPythonPth(runtimeDir);
        await PythonPipBootstrapper.EnsurePipAsync(pythonExe, runtimeDir, cancellationToken).ConfigureAwait(false);
        return pythonExe;
    }

    private static void PatchEmbeddedPythonPth(string runtimeDir)
    {
        var pthFile = Directory.GetFiles(runtimeDir, "python*._pth");
        if (pthFile.Length == 0)
            return;

        var path = pthFile[0];
        var lines = new List<string>(File.ReadAllLines(path));

        var hasSitePackages = false;
        var siteLineIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Equals("Lib\\site-packages", StringComparison.OrdinalIgnoreCase))
                hasSitePackages = true;
            if (line.Equals("import site", StringComparison.Ordinal))
                siteLineIndex = i;
            if (line.Equals("#import site", StringComparison.Ordinal))
                siteLineIndex = i;
        }

        if (!hasSitePackages)
            lines.Insert(0, "Lib\\site-packages");

        if (siteLineIndex >= 0)
            lines[siteLineIndex] = "import site";
        else
            lines.Add("import site");

        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", Encoding.UTF8);
    }

    private static void ExtractZipOverwrite(string zipPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
                continue;

            var destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            var fullTargetDir = Path.GetFullPath(targetDir);
            if (!destinationPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                continue;

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? targetDir);
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            entryStream.CopyTo(fileStream);
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
