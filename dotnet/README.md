## DeepSeek.OCR2 (.NET / NuGet) 封装说明

这个 NuGet 包提供两层封装：

1. `DeepSeekOcr2LocalServer`：从包内释放一个轻量 Python HTTP Server 脚本并启动子进程（模型只加载一次，便于多次调用）。
2. `DeepSeekOcr2Client`：通过 HTTP 调用 `POST /ocr` 执行 OCR 推理。

### 先决条件

- Windows：无需预装 Python。本包默认会在首次运行时自动下载便携版 Python（默认 3.10.11）并引导 pip/venv。
- Linux/macOS：建议预装 Python 3.10+（或手动指定 `PythonExecutablePath`）。
- 推理依赖（`torch`/`transformers` 等）默认会在首次运行时自动创建 venv 并安装（CPU 预设）；也支持离线 wheels（见下文）。
- 如果希望更快推理，可按上游说明安装 `flash-attn`（否则服务端会自动降级）。

### 依赖“打包/自带”能做到什么

- 默认模式：首次运行时自动创建 venv 并通过 pip 安装依赖（Windows 可自动下载便携 Python）。
- 离线全量模式：把 **Python runtime + wheels/torch + 模型权重** 一起打进同一个 `.nupkg`（包体会非常大，通常只能发布到私有 NuGet 源）。

### 许可证与归属

- 上游仓库 DeepSeek-OCR-2 的许可证为 Apache License 2.0（见仓库根目录 LICENSE.txt）。
- 本 NuGet 包仅做 .NET 调用封装与启动脚本分发，不包含模型权重；模型与其使用条款请以 deepseek-ai/DeepSeek-OCR-2（HuggingFace/上游仓库）为准。
- 本封装仓库地址：https://github.com/ichichchch/DeepSeekOCR2.NET

### 最小用法（启动本地服务 + 调用 OCR）

```csharp
using DeepSeek.OCR2;

var result = await DeepSeekOcr2.RecognizeFileAsync(@"D:\test.jpg");
Console.WriteLine(result.Text);
```

如需复用同一个模型进程（多次调用更快）：

```csharp
using DeepSeek.OCR2;

await using var session = await DeepSeekOcr2.CreateSessionAsync();

var request = DeepSeekOcr2Request.FromFile(@"D:\test.jpg") with { Prompt = "<image>\nFree OCR." };
var result = await session.Client.RecognizeAsync(request);
Console.WriteLine(result.Text);
```

### Torch 自动安装选项

- `TorchInstallPreset`
  - `Cpu`：按 PyTorch 官方 CPU 索引安装（默认）
  - `None`：不安装 torch（适合你自行管理 Python 环境）
  - `Cuda118`：按 PyTorch 官方 cu118 索引安装（与本仓库 README 示例一致）
- `OfflineWheelDirectory`：指定离线 wheel 目录（会传给 pip：`--find-links <dir>`）
- `PreferOfflineWheels`：为 true 时额外加 `--no-index`（强制只从离线目录找）
- `TorchVersion/TorchVisionVersion/TorchAudioVersion`：默认 `2.6.0/0.21.0/2.6.0`，可自行改

### 目标框架

- DeepSeek.OCR2：netstandard2.0 / net6.0 / net8.0 / net10.0

### 发布到 nuget.org（建议）

- **Owners**：nuget.org 的包所有者最终由你上传时使用的账号/组织决定；建议用你的组织账号作为 owner，并通过 nuget.org 后台添加/移除 owners。工程里的 `Owners` 字段仅作为元数据展示用（不同站点可能忽略）。
- **RepositoryBranch/Commit**：本包在 CI（GitHub Actions）环境下会自动读取 `GITHUB_REF_NAME` / `GITHUB_SHA` 并写入包元数据；也支持在打包命令里显式覆盖：
  - `dotnet pack -p:RepositoryBranch=main -p:RepositoryCommit=<commitSha>`
- **自动发布（GitHub Actions）**：
  - 在仓库 Secrets 添加 `NUGET_API_KEY`（nuget.org 生成的 API Key）
  - 推送 tag `v*`（例如 `v0.1.7`）会触发发布工作流 `nuget-publish`

### 本地打包/推送

- 打包（元包+依赖包）：`pwsh .\pack.ps1`（输出到 `dotnet/artifacts/`）
- 推送到 nuget.org：`pwsh .\push.ps1 -ApiKey <key>`（默认推送 `DeepSeek.OCR2*` 相关包；不包含 `DeepSeek.OCR2.Bundled` 单包超大资产方案与 `DeepSeek.OCR2.Full.win-x64` 兼容包）
- 如需推送其他包：使用 `-PackageGlob` 显式指定

### 发布到 nuget.org（NuGet Gallery）

- 本仓库的 GitHub Actions 工作流 `nuget-publish` 会在推送 tag `v*` 时打包并推送 `DeepSeek.OCR2`（元包）以及其依赖包（`DeepSeek.OCR2.Core`、`DeepSeek.OCR2.Assets.*`）。
- 不会推送 `DeepSeek.OCR2.Bundled`（单包超大资产方案）与 `DeepSeek.OCR2.Full.win-x64`（历史兼容包）。
- 发布后会自动把 `DeepSeek.OCR2.Core` 与 `DeepSeek.OCR2.Assets.*` 设为 Unlisted（用户搜索只看到 `DeepSeek.OCR2`，但依赖仍可正常还原）。
- 需要在仓库 Secrets 配置 `NUGET_API_KEY`。

也可以本地发布（会打包、推送、并可选 Unlist 内部包）：

```powershell
pwsh .\publish-nuget.ps1 -Version 0.3.0
```

### 离线全量包（模型+torch+wheels+Python runtime）

可以拆成多个 NuGet 包来实现“全量离线”，优点是：每个包体积可控、可按需选择（例如不同平台/不同 torch 版本）；缺点是：发布/版本管理更复杂，下载包数量更多。

推荐引用方式（win-x64）：

- 只引用一个包：`DeepSeek.OCR2`（会自动拉起 `DeepSeek.OCR2.Core` + Python/wheels/模型资源包）
- 如只要在线安装（不带离线资产）：引用 `DeepSeek.OCR2.Core`

目录结构（会被打入 `.nupkg` 并在引用方输出目录自动复制到 `DeepSeek.OCR2/bundled/`）：

- `dotnet/src/DeepSeek.OCR2/Bundled/python/<rid>/<version>/...`
- `dotnet/src/DeepSeek.OCR2/Bundled/wheels/<rid>/*.whl`
- `dotnet/src/DeepSeek.OCR2/Bundled/models/DeepSeek-OCR-2/...`

准备资产（会下载大量内容）：

```powershell
pwsh .\bundle\prepare-bundled-assets.ps1 -TorchPreset cpu -ModelId deepseek-ai/DeepSeek-OCR-2
```

随后打包：

```powershell
pwsh .\pack.ps1
```

运行时行为：

- 若检测到 `DeepSeek.OCR2/bundled/python/.../python.exe`：优先使用随包 Python，不再下载。
- 若检测到 `DeepSeek.OCR2/bundled/wheels/<rid>`：默认作为离线 wheel 源（`--no-index --find-links`）。
- 若 `ModelName` 仍为默认 `deepseek-ai/DeepSeek-OCR-2` 且存在 `DeepSeek.OCR2/bundled/models/DeepSeek-OCR-2`：优先加载离线模型目录。

### HTTP 协议

- `GET /health`：健康检查（返回 `{ "ok": true }`）
- `POST /ocr`：JSON 请求体（关键字段）
  - `image_base64`：图片内容（Base64）
  - `prompt`：提示词（需要包含 `<image>`）
  - `output_dir`：可选，输出目录
  - `base_size` / `image_size` / `crop_mode` / `save_results`：与官方 `model.infer` 参数一致
