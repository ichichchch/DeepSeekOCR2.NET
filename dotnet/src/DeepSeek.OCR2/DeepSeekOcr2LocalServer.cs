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

        var host = string.IsNullOrWhiteSpace(options.Host) ? "127.0.0.1" : options.Host.Trim();
        var port = options.Port > 0 ? options.Port : GetFreeTcpPort(host);
        var modelName = options.ModelName;
        if (string.Equals(modelName, "deepseek-ai/DeepSeek-OCR-2", StringComparison.Ordinal))
        {
            var bundled = DeepSeekOcr2BundledAssets.TryGetBundledModelDirectory();
            if (!string.IsNullOrWhiteSpace(bundled))
                modelName = bundled!;
        }

        var workingDir = string.IsNullOrWhiteSpace(options.WorkingDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeepSeek.OCR2",
                "server")
            : options.WorkingDirectory!.Trim();

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

            if (string.Equals(options.Device, "cuda", StringComparison.OrdinalIgnoreCase) &&
                options.TorchInstallPreset == DeepSeekOcr2TorchInstallPreset.Cpu &&
                string.IsNullOrWhiteSpace(options.OfflineWheelDirectory))
            {
                throw new InvalidOperationException(
                    "Device is set to 'cuda' but TorchInstallPreset is 'Cpu' (and no OfflineWheelDirectory was provided). " +
                    "This will install a CPU-only torch build and the server will fail with 'Torch not compiled with CUDA enabled'. " +
                    "Fix: set TorchInstallPreset=Cuda118, or set TorchInstallPreset=None and manage a CUDA-enabled torch in your Python environment, " +
                    "or provide OfflineWheelDirectory that contains CUDA-enabled torch wheels.");
            }

            var pythonWorkingDir = Path.GetDirectoryName(pythonExe) ?? workingDir;
            var supportsVenv = await SupportsVenvAsync(pythonExe, pythonWorkingDir, cancellationToken).ConfigureAwait(false);

            if (supportsVenv)
            {
                var venvDir = string.IsNullOrWhiteSpace(options.VenvDirectory)
                    ? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "DeepSeek.OCR2",
                        "venv")
                    : options.VenvDirectory!.Trim();

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
            else
            {
                await PythonPipBootstrapper.EnsurePipAsync(
                    pythonExe,
                    pythonWorkingDir,
                    options.BootstrapDownloadTimeout,
                    cancellationToken).ConfigureAwait(false);

                await RunPythonAsync(
                    pythonExe,
                    pythonWorkingDir,
                    new[] { "-m", "pip", "install", "--upgrade", "pip" },
                    cancellationToken).ConfigureAwait(false);

                var basePipArgs = BuildPipCommonArgs(options);

                if (options.TorchInstallPreset != DeepSeekOcr2TorchInstallPreset.None)
                {
                    var torchArgs = new System.Collections.Generic.List<string> { "-m", "pip", "install" };
                    torchArgs.AddRange(BuildTorchInstallArgs(options));
                    torchArgs.AddRange(basePipArgs);
                    await RunPythonAsync(pythonExe, pythonWorkingDir, torchArgs, cancellationToken).ConfigureAwait(false);
                }

                var runtimeReq = EmbeddedPythonScripts.ExtractRuntimeRequirements(workingDir);
                var reqArgs = new System.Collections.Generic.List<string> { "-m", "pip", "install", "-r", runtimeReq };
                reqArgs.AddRange(basePipArgs);
                if (options.PipInstallArguments is { Length: > 0 })
                    reqArgs.AddRange(options.PipInstallArguments);

                await RunPythonAsync(pythonExe, pythonWorkingDir, reqArgs, cancellationToken).ConfigureAwait(false);
            }
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

        var offlineWheelDirectory = options.OfflineWheelDirectory;
        if (!string.IsNullOrWhiteSpace(offlineWheelDirectory))
        {
            if (options.PreferOfflineWheels)
                args.Add("--no-index");
            args.Add("--find-links");
            args.Add(offlineWheelDirectory!);
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
                args.Add(indexUrl!);
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

    private static async Task<bool> SupportsVenvAsync(string pythonExe, string workingDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            ProcessUtil.AddArguments(psi, new[] { "-c", "import venv" });

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

    private static async Task RunPythonAsync(
        string pythonExe,
        string workingDirectory,
        System.Collections.Generic.IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        ProcessUtil.AddArguments(psi, arguments);

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
            throw new InvalidOperationException($"Process failed: {pythonExe}. ExitCode={process.ExitCode}. Output: {output}");
        }
    }
}
