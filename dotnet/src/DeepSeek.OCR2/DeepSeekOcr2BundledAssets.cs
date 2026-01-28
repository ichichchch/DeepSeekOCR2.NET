using System;
using System.IO;

namespace DeepSeek.OCR2;

internal static class DeepSeekOcr2BundledAssets
{
    private static string? TryGetBundledRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
            return null;

        var candidate = Path.Combine(baseDir, "DeepSeek.OCR2", "bundled");
        return Directory.Exists(candidate) ? candidate : null;
    }

    public static string? TryGetBundledModelDirectory()
    {
        var root = TryGetBundledRoot();
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var candidate = Path.Combine(root, "models", "DeepSeek-OCR-2");
        return Directory.Exists(candidate) ? candidate : null;
    }

    public static string? TryGetBundledWheelsDirectory()
    {
        var root = TryGetBundledRoot();
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var rid = "win-x64";
        var candidate = Path.Combine(root, "wheels", rid);
        return Directory.Exists(candidate) ? candidate : null;
    }

    public static string? TryGetBundledPythonExecutable()
    {
        var root = TryGetBundledRoot();
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var rid = "win-x64";
        var pythonRoot = Path.Combine(root, "python", rid);
        if (!Directory.Exists(pythonRoot))
            return null;

        foreach (var dir in Directory.GetDirectories(pythonRoot))
        {
            var exe = Path.Combine(dir, "python.exe");
            if (File.Exists(exe))
                return exe;
        }

        var direct = Path.Combine(pythonRoot, "python.exe");
        return File.Exists(direct) ? direct : null;
    }
}
