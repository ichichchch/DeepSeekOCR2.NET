<!-- markdownlint-disable first-line-h1 -->

# DeepSeekOCR2.NET

DeepSeekOCR2.NET 是对 **DeepSeek-OCR-2** 的 .NET 封装：通过启动一个本地 Python HTTP 推理服务，并由 .NET 客户端以 HTTP 调用完成 OCR 识别。

- 上游模型（Hugging Face）：https://huggingface.co/deepseek-ai/DeepSeek-OCR-2
- 上游论文（PDF）：https://github.com/deepseek-ai/DeepSeek-OCR-2/blob/main/DeepSeek_OCR2_paper.pdf

## 快速开始（.NET）

安装 NuGet（建议从下方“包结构”里按需选择）后：

```csharp
using DeepSeek.OCR2;

var result = await DeepSeekOcr2.RecognizeFileAsync(@"D:\test.jpg");
Console.WriteLine(result.Text);
```

复用同一个模型进程（多次调用更快）：

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

更完整的 .NET 使用说明、配置项与故障排查请看：[dotnet/README.md](dotnet/README.md)。

## 包结构（你应该引用哪个）

推荐只记住两种用法：

- 在线/自动安装（包体小）：引用 `DeepSeek.OCR2.Core`
- 离线/可选资产（更省心）：引用 `DeepSeek.OCR2`（meta 包，会自动拉取资产包）

仓库中会发布这些包：

- `DeepSeek.OCR2.Core`：.NET 客户端 + 本地 Python 服务引导（默认不含模型权重）
- `DeepSeek.OCR2`：meta 包 = Core + `DeepSeek.OCR2.Assets.*`（离线 Python / wheels / 模型）
- `DeepSeek.OCR2.Assets.Python.win-x64`：Windows 便携 Python（可选）
- `DeepSeek.OCR2.Assets.Wheels.win-x64`：离线 wheels/torch（可选）
- `DeepSeek.OCR2.Assets.Model.DeepSeekOCR2`：模型快照（可选）
- `DeepSeek.OCR2.Bundled`：单包内包含 python+wheels+模型的离线分发方案（包体非常大，通常建议私有源）

## 离线与 Bundled 资产（重要）

本仓库的 `dotnet/src/DeepSeek.OCR2/Bundled/*` 为 **生成型资产目录**（便携 Python / wheels / 模型快照），默认不提交到 Git。

- GitHub LFS 对单文件有 2GB 上限；DeepSeek-OCR-2 的 safetensors 权重文件可能大于 2GB，直接推送会失败。
- 建议做法：
  - 生产/发布场景：通过 CI 或本地脚本生成 Bundled 资产，然后打包成 NuGet（例如 `DeepSeek.OCR2.Bundled`），并发布到私有源；
  - 开发/调试场景：仅在本地生成，或使用 `DeepSeek.OCR2` + Assets 包组合。

准备 Bundled 资产（会下载大量内容）：

```powershell
pwsh .\dotnet\bundle\prepare-bundled-assets.ps1 -TorchPreset cpu -ModelId deepseek-ai/DeepSeek-OCR-2
```

随后打包：

```powershell
pwsh .\dotnet\pack.ps1 -PackBundled
```

## 许可证与致谢

- 本仓库为 .NET 封装与打包工程；模型与论文归上游项目所有。
- 致谢与引用请参考上游 DeepSeek-OCR-2 仓库与论文。
