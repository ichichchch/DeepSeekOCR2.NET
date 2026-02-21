# DeepSeek.OCR2 (.NET / NuGet)

DeepSeek.OCR2 是一个高性能 OCR 识别库，基于 **DeepSeek-OCR-2** 模型构建，提供简洁的 .NET API 接口。

- `DeepSeekOcr2` / `DeepSeekOcr2Session`：一键启动/复用服务的便捷入口
- `DeepSeekOcr2Client`：执行 OCR 识别的客户端
- `DeepSeekOcr2LocalServer`：本地服务管理器（自动处理依赖和模型加载）

## 安装

推荐使用 `DeepSeek.OCR2` 包（自动包含所需依赖）：

```powershell
dotnet add package DeepSeek.OCR2
```

或者使用核心包（更小的包体，首次运行会自动下载所需组件）：

```powershell
dotnet add package DeepSeek.OCR2.Core
```

### 可用包说明

- `DeepSeek.OCR2.Core`：核心客户端 + 自动环境配置（默认在线安装依赖）
- `DeepSeek.OCR2`：完整包 = Core + 离线资产包（Python / wheels / 模型）
- `DeepSeek.OCR2.Assets.Python.win-x64`：Windows 便携 Python 环境（可选）
- `DeepSeek.OCR2.Assets.Wheels.win-x64`：离线依赖包（可选）
- `DeepSeek.OCR2.Assets.Model`：模型快照（可选）
- `DeepSeek.OCR2.Bundled`：单包全量离线版本（包体较大，适合私有源）

## 先决条件

- **Windows**：无需预装 Python，首次运行会自动配置所需环境
- **Linux/macOS**：建议预装 Python 3.10+
- **首次运行**：会自动下载模型和依赖项，请耐心等待

## 快速开始

### 基本使用

识别一张图片：

```csharp
using DeepSeek.OCR2;

var result = await DeepSeekOcr2.RecognizeFileAsync(@"D:\test.jpg");
Console.WriteLine(result.Text);
```

### 复用会话（性能更优）

多次调用时复用模型实例，速度更快：

```csharp
using DeepSeek.OCR2;

await using var session = await DeepSeekOcr2.CreateSessionAsync();

var request = DeepSeekOcr2Request.FromFile(@"D:\test.jpg") with 
{
    Prompt = "<image>\nFree OCR."
};

var result = await session.Client.RecognizeAsync(request);
Console.WriteLine(result.Text);
```

## 常用配置

### 基础配置示例

```csharp
using System;
using DeepSeek.OCR2;

var options = new DeepSeekOcr2LocalServerOptions
{
    // 设备选择："cpu" 或 "cuda"
    Device = "cpu",
    
    // 计算精度："float32" 或 "float16"
    DType = "float32",
    
    // 超时设置
    OcrRequestTimeout = TimeSpan.FromMinutes(30),
};

await using var session = await DeepSeekOcr2.CreateSessionAsync(options);
var result = await session.Client.RecognizeAsync(
    DeepSeekOcr2Request.FromFile(@"D:\test.jpg")
);
```

### 超时设置

- `OcrRequestTimeout`：单次 OCR 请求超时（默认 30 分钟）
- `BootstrapDownloadTimeout`：首次启动时下载组件的超时时间
- `StartupTimeout`：等待服务启动的超时时间

### 性能优化

**GPU 加速**（需要 CUDA 环境）：

```csharp
var options = new DeepSeekOcr2LocalServerOptions
{
    Device = "cuda",           // 使用 GPU
    DType = "float16",         // 降低显存占用
};
```

### 高级配置

**自定义模型和端口**：

```csharp
var options = new DeepSeekOcr2LocalServerOptions
{
    ModelName = "deepseek-ai/DeepSeek-OCR-2",  // 模型名称
    Host = "localhost",                         // 监听地址
    Port = 0,                                   // 0=自动找空闲端口
};
```

**离线模式**：

```csharp
var options = new DeepSeekOcr2LocalServerOptions
{
    OfflineWheelDirectory = @"C:\wheels",       // 离线依赖目录
    PreferOfflineWheels = true,                 // 强制使用离线依赖
};
```

## 配置模板（contentFiles）

Core 包会把默认配置模板拷贝到引用方输出目录：

- `DeepSeek.OCR2/templates/deepseek-ocr2.defaults.json`

## 离线/资产包的运行时行为

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

## 故障排查

### 首次运行很慢

首次启动需要下载模型和依赖项，可能需要较长时间。后续运行将直接使用本地缓存，速度会快很多。

如果担心超时，可以调大超时时间：

```csharp
var options = new DeepSeekOcr2LocalServerOptions
{
    OcrRequestTimeout = TimeSpan.FromMinutes(60),
};
```

### 内存不足错误

调试时如遇到内存相关错误提示：

- 关闭"启用诊断工具 (调试时)"
- 或先用 Ctrl+F5 跑通首次启动，再附加调试

### GPU 相关问题

如果设置 `Device="cuda"` 但报错，可能是因为：

- 当前环境没有安装 CUDA 版本的依赖
- 解决方法：改用 `Device="cpu"` 或使用完整包自动配置 CUDA 环境

## 工作原理

DeepSeek.OCR2 在本地启动一个优化的推理服务，.NET 客户端通过本地 HTTP 接口调用：

- **自动配置**：首次运行自动准备环境和依赖
- **智能缓存**：模型和依赖缓存在本地，后续离线使用
- **进程复用**：`session` 模式可复用模型实例，大幅提升性能
- **健康检查**：自动检测服务状态，确保可用性

HTTP 接口：

- `GET /health`：健康检查
- `POST /ocr`：执行 OCR 识别

## 仓库维护者：本地打包 / 发布到 nuget.org

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
$env:NUGET_API_KEY = "<your-nuget-api-key>"
pwsh .\dotnet\push.ps1
```

GitHub Actions 自动发布：

- 在仓库 Secrets 配置 `NUGET_API_KEY`
- 推送 tag `v*` 会触发发布工作流

```powershell
git tag v0.3.8
git push origin v0.3.8
```

## 许可证与归属

- 本项目基于 DeepSeek-OCR-2 模型构建
- 上游项目许可证：Apache License 2.0
- 本封装项目地址：https://github.com/ichichchch/DeepSeekOCR2.NET
