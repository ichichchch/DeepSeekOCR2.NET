## DeepSeek.OCR2 (.NET / NuGet) 封装说明

这个 NuGet 包提供两层封装：

1. `DeepSeekOcr2LocalServer`：从包内释放一个轻量 Python HTTP Server 脚本并启动子进程（模型只加载一次，便于多次调用）。
2. `DeepSeekOcr2Client`：通过 HTTP 调用 `POST /ocr` 执行 OCR 推理。

### 先决条件

- 已安装 Python（建议 3.10+，官方示例环境为 3.12）
- 推理依赖（`torch`/`transformers` 等）可以由你预装，也可以让本包启动时自动创建 venv 并安装一部分依赖（见下文）
- 如果希望更快推理，按官方 README 安装 `flash-attn`（否则服务端会自动降级到不指定 `_attn_implementation` 的加载方式）

### 依赖“打包/自带”能做到什么

- 纯把 `torch` / CUDA / `flash-attn` 这类依赖“直接塞进 NuGet”在体积和平台适配上不现实（Windows/Linux、CPU/CUDA 版本差异、wheel 很大）。
- 这里提供的折中方案是：NuGet **内置 requirements 列表**，启动时可选 **自动创建 venv + pip 安装依赖**（仍然需要你机器上有 Python；安装 torch 可能需要网络/指定镜像/指定 CUDA 轮子）。

### 许可证与归属

- 上游仓库 DeepSeek-OCR-2 的许可证为 Apache License 2.0（见仓库根目录 LICENSE.txt）。
- 本 NuGet 包仅做 .NET 调用封装与启动脚本分发，不包含模型权重；模型与其使用条款请以 deepseek-ai/DeepSeek-OCR-2（HuggingFace/上游仓库）为准。
- 本封装仓库地址：https://github.com/ichichchch/DeepSeekOCR2.NET

### 最小用法（启动本地服务 + 调用 OCR）

```csharp
using DeepSeek.OCR2;

await using var server = await DeepSeekOcr2LocalServer.StartAsync(new DeepSeekOcr2LocalServerOptions
{
    PythonExecutablePath = "python",
    EnsureVenv = true,
    TorchInstallPreset = DeepSeekOcr2TorchInstallPreset.Cuda118,
    ModelName = "deepseek-ai/DeepSeek-OCR-2",
    Device = "cuda",
    PipInstallArguments = new[]
    {
        "--upgrade",
    },
});

using var http = new HttpClient { BaseAddress = server.BaseUri };
var client = new DeepSeekOcr2Client(http);

var request = DeepSeekOcr2Request.FromFile(@"D:\test.jpg") with
{
    Prompt = "<image>\nFree OCR.",
    SaveResults = true,
};

var result = await client.RecognizeAsync(request);
Console.WriteLine(result.Text);
```

### Torch 自动安装选项

- `TorchInstallPreset`
  - `None`：不安装 torch（默认）
  - `Cpu`：按 PyTorch 官方 CPU 索引安装
  - `Cuda118`：按 PyTorch 官方 cu118 索引安装（与本仓库 README 示例一致）
- `OfflineWheelDirectory`：指定离线 wheel 目录（会传给 pip：`--find-links <dir>`）
- `PreferOfflineWheels`：为 true 时额外加 `--no-index`（强制只从离线目录找）
- `TorchVersion/TorchVisionVersion/TorchAudioVersion`：默认 `2.6.0/0.21.0/2.6.0`，可自行改

### 目标框架

- DeepSeek.OCR2：net6.0 / net8.0 / net10.0

### 发布到 nuget.org（建议）

- **Owners**：nuget.org 的包所有者最终由你上传时使用的账号/组织决定；建议用你的组织账号作为 owner，并通过 nuget.org 后台添加/移除 owners。工程里的 `Owners` 字段仅作为元数据展示用（不同站点可能忽略）。
- **RepositoryBranch/Commit**：本包在 CI（GitHub Actions）环境下会自动读取 `GITHUB_REF_NAME` / `GITHUB_SHA` 并写入包元数据；也支持在打包命令里显式覆盖：
  - `dotnet pack -p:RepositoryBranch=main -p:RepositoryCommit=<commitSha>`
- **自动发布（GitHub Actions）**：
  - 在仓库 Secrets 添加 `NUGET_API_KEY`（nuget.org 生成的 API Key）
  - 推送 tag `v*`（例如 `v0.1.7`）会触发发布工作流 `nuget-publish`

### HTTP 协议

- `GET /health`：健康检查（返回 `{ "ok": true }`）
- `POST /ocr`：JSON 请求体（关键字段）
  - `image_base64`：图片内容（Base64）
  - `prompt`：提示词（需要包含 `<image>`）
  - `output_dir`：可选，输出目录
  - `base_size` / `image_size` / `crop_mode` / `save_results`：与官方 `model.infer` 参数一致
