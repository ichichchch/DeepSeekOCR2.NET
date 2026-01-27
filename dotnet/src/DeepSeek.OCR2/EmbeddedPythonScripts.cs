using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DeepSeek.OCR2;

internal static class EmbeddedPythonScripts
{
    private const string ServerFileName = "deepseek_ocr2_http_server.py";
    private const string RuntimeRequirementsFileName = "requirements_runtime.txt";

    public static string ExtractServerScript(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory is required.", nameof(targetDirectory));

        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, ServerFileName);
        if (File.Exists(targetPath))
            return targetPath;

        var assembly = typeof(EmbeddedPythonScripts).GetTypeInfo().Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".Python.{ServerFileName}", StringComparison.Ordinal));

        if (resourceName is null)
            throw new InvalidOperationException($"Embedded resource not found: {ServerFileName}");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Failed to open embedded resource: {resourceName}");

        using var file = File.OpenWrite(targetPath);
        stream.CopyTo(file);
        return targetPath;
    }

    public static string ExtractRuntimeRequirements(string targetDirectory)
    {
        return ExtractFile(targetDirectory, RuntimeRequirementsFileName);
    }

    private static string ExtractFile(string targetDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory is required.", nameof(targetDirectory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, fileName);
        if (File.Exists(targetPath))
            return targetPath;

        var assembly = typeof(EmbeddedPythonScripts).GetTypeInfo().Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".Python.{fileName}", StringComparison.Ordinal));

        if (resourceName is null)
            throw new InvalidOperationException($"Embedded resource not found: {fileName}");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Failed to open embedded resource: {resourceName}");

        using var file = File.OpenWrite(targetPath);
        stream.CopyTo(file);
        return targetPath;
    }
}
