## DeepSeek.OCR2 (.NET / NuGet) 使用说明

DeepSeek.OCR2 提供一个“本地 Python 推理服务 + .NET 客户端”的封装：

- `DeepSeekOcr2LocalServer`：从包内释放 Python HTTP Server 脚本，按需准备 Python/venv/依赖，然后启动子进程（模型进程可复用）。
- `DeepSeekOcr2Client`：通过 HTTP 调用 `POST /ocr` 执行 OCR 推理。
- `DeepSeekOcr2` / `DeepSeekOcr2Session`：一键启动/复用服务的便捷入口。

### 包结构（你应该引用哪个）

推荐只记住两种用法：

- 在线/自动安装（包体小）：引用 `DeepSeek.OCR2.Core`
- 离线/可选资产（更省心）：引用 `DeepSeek.OCR2`（meta 包，会自动拉取资产包）

仓库中实际会发布这些包：

- `DeepSeek.OCR2.Core`：.NET 客户端 + 本地 Python 服务引导（默认不含模型权重）
- `DeepSeek.OCR2`：meta 包 = Core + `DeepSeek.OCR2.Assets.*`（离线 Python / wheels / 模型）
- `DeepSeek.OCR2.Assets.Python.win-x64`：Windows 便携 Python（可选）
- `DeepSeek.OCR2.Assets.Wheels.win-x64`：离线 wheels/torch（可选）
- `DeepSeek.OCR2.Assets.Model`：模型快照（可选）
- `DeepSeek.OCR2.Bundled`：单包内包含 python+wheels+模型的离线分发方案（包体可能非常大，通常建议私有源）
- `DeepSeek.OCR2.Full.win-x64`：历史等价包，已停止发布新版本

### 先决条件

- Windows：默认无需预装 Python。`AutoSetupPython=true` 时会在首次运行自动下载便携 Python 并引导 pip/venv。
- Linux/macOS：建议预装 Python 3.10+，并用 `PythonExecutablePath` 指定；或自行关闭 `AutoSetupPython/EnsureVenv` 并管理环境。
- 依赖安装：默认会创建 venv 并安装 torch + runtime requirements；也支持离线 wheels（见下文）。

### 快速开始

最简单：识别一张图片文件（内部自动启动本地服务）：

```csharp
using DeepSeek.OCR2;

var result = await DeepSeekOcr2.RecognizeFileAsync(@"D:\test.jpg");
Console.WriteLine(result.Text);
```

复用同一个模型进程（多次调用更快）：

```csharp
using DeepSeek.OCR2;

await using var session = await DeepSeekOcr2.CreateSessionAsync();

var request = DeepSeekOcr2Request.FromFile(@"D:\test.jpg") with { Prompt = "<image>\nFree OCR." };
var result = await session.Client.RecognizeAsync(request);
Console.WriteLine(result.Text);
```

### 常用配置（DeepSeekOcr2LocalServerOptions）

超时与首启动：

- `OcrRequestTimeout`：单次 OCR 请求超时（默认 30 分钟）。设为 `<= 0` 会变成无限超时（`Timeout.InfiniteTimeSpan`）。
- `BootstrapDownloadTimeout`：下载便携 Python / 其它引导步骤的超时。
- `StartupTimeout`：等待 `GET /health` 变为可用的超时。

Python/venv：

- `PythonExecutablePath`：指定系统 Python（Linux/macOS 常用）。
- `AutoSetupPython`：是否自动下载/准备 Windows 便携 Python。
- `EnsureVenv` / `VenvDirectory`：是否创建并使用 venv。

Torch 安装与离线 wheels：

- `TorchInstallPreset`：`Cpu`（默认）/ `Cuda118` / `None`
- `OfflineWheelDirectory`：离线 wheel 目录（会传给 pip：`--find-links <dir>`）
- `PreferOfflineWheels`：为 true 时额外加 `--no-index`（强制只从离线目录找）
- `TorchVersion/TorchVisionVersion/TorchAudioVersion`：按需锁定版本

模型与推理参数（传给 Python 服务）：

- `ModelName`：默认 `deepseek-ai/DeepSeek-OCR-2`
- `Device`：默认 `cpu`
- `DType`：默认 `float32`
- `AttnImpl`：默认 `sdpa`
- `Host` / `Port`：本地服务监听地址与端口（`Port=0` 自动找空闲端口）

性能与 GPU（高级用法）：

- `Device="cuda"`：使用 GPU（前提：你的 Python 环境能用 CUDA）
- `DType="float16"` / `DType="bfloat16"`：降低显存占用、可能更快
- `AttnImpl="flash_attention_2"`：如环境支持可更快；不支持时服务端会自动降级

### 配置模板（contentFiles）

Core 包会把默认配置模板拷贝到引用方输出目录：

- `DeepSeek.OCR2/templates/deepseek-ocr2.defaults.json`

### 离线/资产包的运行时行为

只要你的输出目录下存在这些路径（由 `DeepSeek.OCR2` meta 包或 `DeepSeek.OCR2.Bundled` 自动拷贝），运行时会自动优先使用离线资源：

- `DeepSeek.OCR2/bundled/python/.../python.exe` 存在：优先使用随包 Python，不再下载
- `DeepSeek.OCR2/bundled/wheels/win-x64/*.whl` 存在：自动作为离线 wheel 源（`--find-links`）
- `DeepSeek.OCR2/bundled/models/DeepSeek-OCR-2` 存在且包含模型文件：当 `ModelName` 为默认值时，自动改为加载该离线目录

目录结构（会被打入 `.nupkg` 并在引用方输出目录自动复制到 `DeepSeek.OCR2/bundled/`）：

- `dotnet/src/DeepSeek.OCR2/Bundled/python/<rid>/<version>/...`
- `dotnet/src/DeepSeek.OCR2/Bundled/wheels/<rid>/*.whl`
- `dotnet/src/DeepSeek.OCR2/Bundled/models/DeepSeek-OCR-2/...`

准备 bundled 资产（会下载大量内容）：

```powershell
pwsh .\dotnet\bundle\prepare-bundled-assets.ps1 -TorchPreset cpu -ModelId deepseek-ai/DeepSeek-OCR-2
```

随后打包：

```powershell
pwsh .\dotnet\pack.ps1 -PackBundled
```

### 故障排查

首次调用很慢/超时：

- 首次启动可能需要创建 venv、安装依赖、下载模型并完成初始化，耗时可能超过默认 HttpClient 100 秒。把 `OcrRequestTimeout` 调大即可。

Visual Studio 调试时弹窗 “DotNetDebugServicesOutOfMemory”：

- 多见于调试器/诊断工具在采集大量大对象分配时自身内存耗尽。
- 建议关闭“启用诊断工具(调试时)”或用 Ctrl+F5 跑通首启动，再附加调试。

离线 wheels 不生效：

- 确认 `OfflineWheelDirectory` 指向目录内真的有 `.whl` 文件。
- 如需要严格离线，设置 `PreferOfflineWheels=true`（会加 `--no-index`）。

Torch not compiled with CUDA enabled：

- 你把 `Device` 设成了 `cuda`，但当前 Python 环境里的 torch 是 CPU-only 版本。
- 解决：把 `Device` 改为 `cpu`；或设置 `TorchInstallPreset=Cuda118` 让 venv 安装 CUDA 版 torch；或 `TorchInstallPreset=None` 并自行维护 CUDA torch。

### HTTP 协议

- `GET /health`：健康检查（返回 `{ "ok": true }`）
- `POST /ocr`：JSON 请求体（关键字段）
  - `image_base64`：图片内容（Base64）
  - `prompt`：提示词（通常需要包含 `<image>`）
  - `output_dir`：可选，输出目录
  - `base_size` / `image_size` / `crop_mode` / `save_results`：与官方 `model.infer` 参数一致

### 本地打包 / 发布到 nuget.org

打包（输出到 `dotnet/artifacts/`）：

```powershell
pwsh .\dotnet\pack.ps1 -Configuration Release -Output artifacts
```

发布（推荐，脚本会打包、推送，并把内部依赖包设为 unlisted）：

```powershell
$env:NUGET_API_KEY = "<your-nuget-api-key>"
pwsh .\dotnet\publish-nuget.ps1 -Version x.y.z
```

如你在“系统环境变量/用户环境变量”里设置了 `NUGET_API_KEY`（或用 `setx`），需要重启当前终端/Visual Studio 才会生效。
如果 unlist 阶段返回 403，通常是 API Key 缺少 Unlist 权限，或当前账号不是这些包的 Owner。

跳过发布超大 bundled 包：

```powershell
pwsh .\dotnet\publish-nuget.ps1 -Version x.y.z -IncludeBundled:$false
```

只推送（不做 unlist、不自动 pack，适合你自己控制流程）：

```powershell
pwsh .\dotnet\push.ps1 -ApiKey "<your-nuget-api-key>"
```

GitHub Actions 自动发布：

- 在仓库 Secrets 配置 `NUGET_API_KEY`
- 推送 tag `v*` 会触发发布工作流

### 许可证与归属

- 上游仓库 DeepSeek-OCR-2 的许可证为 Apache License 2.0（见仓库根目录 LICENSE.txt）。
- 本封装仓库地址：https://github.com/ichichchch/DeepSeekOCR2.NET
