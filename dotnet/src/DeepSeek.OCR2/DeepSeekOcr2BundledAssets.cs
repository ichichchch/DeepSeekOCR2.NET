using System;
using System.IO;

namespace DeepSeek.OCR2;

internal static class DeepSeekOcr2BundledAssets
{
    private static bool ContainsAnyFile(string directory, string pattern)
    {
        try
        {
            foreach (var _ in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
                return true;
        }
        catch
        {
        }

        return false;
    }

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
        if (!Directory.Exists(candidate))
            return null;

        if (ContainsAnyFile(candidate, "*.safetensors") || ContainsAnyFile(candidate, "*.bin"))
            return candidate;

        if (File.Exists(Path.Combine(candidate, "config.json")))
            return candidate;

        return null;
    }

    public static string? TryGetBundledWheelsDirectory()
    {
        var root = TryGetBundledRoot();
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var rid = "win-x64";
        var candidate = Path.Combine(root, "wheels", rid);
        if (!Directory.Exists(candidate))
            return null;

        return ContainsAnyFile(candidate, "*.whl") ? candidate : null;
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
