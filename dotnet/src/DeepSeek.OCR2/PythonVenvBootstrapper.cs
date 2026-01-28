using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

internal static class PythonVenvBootstrapper
{
    public static async Task<(string PythonExe, string PipExe)> EnsureVenvAsync(
        string systemPythonExe,
        string venvDir,
        TimeSpan bootstrapDownloadTimeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(systemPythonExe))
            throw new ArgumentException("Python executable path is required.", nameof(systemPythonExe));
        if (string.IsNullOrWhiteSpace(venvDir))
            throw new ArgumentException("Venv directory is required.", nameof(venvDir));

        Directory.CreateDirectory(venvDir);

        var venvPython = GetVenvPythonExe(venvDir);
        var venvPip = GetVenvPipExe(venvDir);

        if (!File.Exists(venvPython))
        {
            await RunAsync(
                fileName: systemPythonExe,
                workingDirectory: venvDir,
                arguments: new[] { "-m", "venv", "--without-pip", venvDir },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await PythonPipBootstrapper.EnsurePipAsync(venvPython, venvDir, bootstrapDownloadTimeout, cancellationToken).ConfigureAwait(false);

        await RunAsync(
            fileName: venvPython,
            workingDirectory: venvDir,
            arguments: new[] { "-m", "pip", "install", "--upgrade", "pip" },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (venvPython, venvPip);
    }

    public static Task PipInstallAsync(
        string venvPipExe,
        string venvDir,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(venvPipExe))
            throw new ArgumentException("pip executable path is required.", nameof(venvPipExe));
        if (string.IsNullOrWhiteSpace(venvDir))
            throw new ArgumentException("Venv directory is required.", nameof(venvDir));
        if (arguments is null || arguments.Count == 0)
            throw new ArgumentException("pip arguments are required.", nameof(arguments));

        var args = new List<string> { "install" };
        args.AddRange(arguments);

        return RunAsync(
            fileName: venvPipExe,
            workingDirectory: venvDir,
            arguments: args,
            cancellationToken: cancellationToken);
    }

    private static string GetVenvPythonExe(string venvDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(venvDir, "Scripts", "python.exe");
        return Path.Combine(venvDir, "bin", "python");
    }

    private static string GetVenvPipExe(string venvDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(venvDir, "Scripts", "pip.exe");
        return Path.Combine(venvDir, "bin", "pip");
    }

    private static async Task RunAsync(
        string fileName,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        ProcessUtil.AddArguments(psi, arguments);

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process: {fileName}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await ProcessUtil.WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"Process failed: {fileName}. ExitCode={process.ExitCode}. Output: {output}");
        }
    }
}
