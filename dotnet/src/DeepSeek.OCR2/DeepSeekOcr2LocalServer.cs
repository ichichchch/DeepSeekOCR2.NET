using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

public sealed class DeepSeekOcr2LocalServer : IAsyncDisposable, IDisposable
{
    private readonly Process _process;
    private bool _disposed;

    private DeepSeekOcr2LocalServer(Uri baseUri, Process process)
    {
        BaseUri = baseUri;
        _process = process;
    }

    public Uri BaseUri { get; }

    public static async Task<DeepSeekOcr2LocalServer> StartAsync(
        DeepSeekOcr2LocalServerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DeepSeekOcr2LocalServerOptions();

        var host = string.IsNullOrWhiteSpace(options.Host) ? "127.0.0.1" : options.Host;
        var port = options.Port > 0 ? options.Port : GetFreeTcpPort(host);
        var modelName = options.ModelName;
        if (string.Equals(modelName, "deepseek-ai/DeepSeek-OCR-2", StringComparison.Ordinal))
        {
            var bundled = DeepSeekOcr2BundledAssets.TryGetBundledModelDirectory();
            if (!string.IsNullOrWhiteSpace(bundled))
                modelName = bundled;
        }

        var workingDir = options.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDir))
        {
            workingDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeepSeek.OCR2",
                "server");
        }

        Directory.CreateDirectory(workingDir);
        var scriptPath = EmbeddedPythonScripts.ExtractServerScript(workingDir);
        var pythonExe = await PythonRuntimeBootstrapper.ResolvePythonExecutableAsync(options, cancellationToken).ConfigureAwait(false);

        if (options.EnsureVenv)
        {
            if (string.IsNullOrWhiteSpace(options.OfflineWheelDirectory))
            {
                var bundledWheels = DeepSeekOcr2BundledAssets.TryGetBundledWheelsDirectory();
                if (!string.IsNullOrWhiteSpace(bundledWheels))
                {
                    options = options with
                    {
                        OfflineWheelDirectory = bundledWheels,
                    };
                }
            }

            var venvDir = options.VenvDirectory;
            if (string.IsNullOrWhiteSpace(venvDir))
            {
                venvDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DeepSeek.OCR2",
                    "venv");
            }

            var (venvPython, venvPip) = await PythonVenvBootstrapper.EnsureVenvAsync(
                systemPythonExe: pythonExe,
                venvDir: venvDir,
                bootstrapDownloadTimeout: options.BootstrapDownloadTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var basePipArgs = BuildPipCommonArgs(options);

            if (options.TorchInstallPreset != DeepSeekOcr2TorchInstallPreset.None)
            {
                var torchArgs = BuildTorchInstallArgs(options);
                torchArgs.AddRange(basePipArgs);
                await PythonVenvBootstrapper.PipInstallAsync(venvPip, venvDir, torchArgs, cancellationToken).ConfigureAwait(false);
            }

            var runtimeReq = EmbeddedPythonScripts.ExtractRuntimeRequirements(workingDir);
            var reqArgs = new System.Collections.Generic.List<string> { "-r", runtimeReq };
            reqArgs.AddRange(basePipArgs);
            if (options.PipInstallArguments is { Length: > 0 })
                reqArgs.AddRange(options.PipInstallArguments);

            await PythonVenvBootstrapper.PipInstallAsync(venvPip, venvDir, reqArgs, cancellationToken).ConfigureAwait(false);

            pythonExe = venvPython;
        }

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        var args = new System.Collections.Generic.List<string>
        {
            scriptPath,
            "--host", host,
            "--port", port.ToString(),
            "--model", modelName,
            "--device", options.Device,
            "--dtype", options.DType,
            "--attn-impl", options.AttnImpl,
        };
        ProcessUtil.AddArguments(psi, args);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start DeepSeek OCR2 python server process.");

        _ = Task.Run(async () =>
        {
            try
            {
                _ = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        });
        _ = Task.Run(async () =>
        {
            try
            {
                _ = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        });

        var baseUri = new UriBuilder(Uri.UriSchemeHttp, host, port).Uri;
        await WaitForReadyAsync(baseUri, options.StartupTimeout, process, cancellationToken).ConfigureAwait(false);

        return new DeepSeekOcr2LocalServer(baseUri, process);
    }

    private static System.Collections.Generic.List<string> BuildPipCommonArgs(DeepSeekOcr2LocalServerOptions options)
    {
        var args = new System.Collections.Generic.List<string>();

        if (!string.IsNullOrWhiteSpace(options.OfflineWheelDirectory))
        {
            if (options.PreferOfflineWheels)
                args.Add("--no-index");
            args.Add("--find-links");
            args.Add(options.OfflineWheelDirectory);
        }

        return args;
    }

    private static System.Collections.Generic.List<string> BuildTorchInstallArgs(DeepSeekOcr2LocalServerOptions options)
    {
        var args = new System.Collections.Generic.List<string>();

        var torchSpec = $"torch=={options.TorchVersion}";
        var visionSpec = $"torchvision=={options.TorchVisionVersion}";
        var audioSpec = $"torchaudio=={options.TorchAudioVersion}";

        args.Add(torchSpec);
        args.Add(visionSpec);
        args.Add(audioSpec);

        if (string.IsNullOrWhiteSpace(options.OfflineWheelDirectory))
        {
            var indexUrl = options.TorchInstallPreset switch
            {
                DeepSeekOcr2TorchInstallPreset.Cpu => "https://download.pytorch.org/whl/cpu",
                DeepSeekOcr2TorchInstallPreset.Cuda118 => "https://download.pytorch.org/whl/cu118",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(indexUrl))
            {
                args.Add("--index-url");
                args.Add(indexUrl);
            }
        }

        return args;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (!_process.HasExited)
#if NET6_0_OR_GREATER
                _process.Kill(entireProcessTree: true);
#else
                _process.Kill();
#endif
        }
        catch
        {
        }

        _process.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    private static int GetFreeTcpPort(string host)
    {
        var ip = IPAddress.Loopback;
        if (IPAddress.TryParse(host, out var parsed))
            ip = parsed;

        var listener = new TcpListener(ip, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForReadyAsync(Uri baseUri, TimeSpan timeout, Process process, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { BaseAddress = EnsureTrailingSlash(baseUri) };
        httpClient.Timeout = TimeSpan.FromSeconds(2);

        var start = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
                throw new InvalidOperationException($"DeepSeek OCR2 server process exited early with code {process.ExitCode}.");

            try
            {
                using var response = await httpClient.GetAsync("health", cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("DeepSeek OCR2 server did not become ready within the startup timeout.");
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var s = uri.ToString();
        return s.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(s + "/", UriKind.Absolute);
    }
}
