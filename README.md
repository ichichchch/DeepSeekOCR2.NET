<!-- markdownlint-disable first-line-h1 -->

# DeepSeekOCR2.NET

DeepSeekOCR2.NET 是对 **DeepSeek-OCR-2** 的 .NET 封装：在本机启动一个 Python HTTP 推理服务，由 .NET 客户端通过 HTTP 调用完成 OCR 识别。

- 上游模型（Hugging Face）：https://huggingface.co/deepseek-ai/DeepSeek-OCR-2
- 上游论文（PDF）：https://github.com/deepseek-ai/DeepSeek-OCR-2/blob/main/DeepSeek_OCR2_paper.pdf
- .NET 详细使用说明（NuGet Readme）：[dotnet/README.md](dotnet/README.md)

## 快速开始（.NET）

安装 NuGet（建议先看下方“包结构”选择包）后：

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

## 包结构（你应该引用哪个）

推荐只记住两种用法（绝大多数用户足够用）：

- 在线/自动安装（包体小）：引用 `DeepSeek.OCR2.Core`
- 离线/可选资产（更省心）：引用 `DeepSeek.OCR2`（meta 包，会自动拉取资产包）

仓库中会发布这些包（了解即可）：

- `DeepSeek.OCR2.Core`：.NET 客户端 + 本地 Python 服务引导（默认不含模型权重）
- `DeepSeek.OCR2`：meta 包 = Core + `DeepSeek.OCR2.Assets.*`（离线 Python / wheels / 模型）
- `DeepSeek.OCR2.Assets.Python.win-x64`：Windows 便携 Python（可选）
- `DeepSeek.OCR2.Assets.Wheels.win-x64`：离线 wheels/torch（可选）
- `DeepSeek.OCR2.Assets.Model`：模型快照（可选）
- `DeepSeek.OCR2.Bundled`：单包内包含 python+wheels+模型的离线分发方案（包体非常大，通常建议私有源）
- `DeepSeek.OCR2.Full.win-x64`：历史等价包，已停止发布新版本

## 离线与 Bundled 资产（重要）

离线有两条路线：

- **推荐（更灵活）**：引用 `DeepSeek.OCR2`（meta 包），让 NuGet 用依赖的方式把 Python / wheels / 模型作为“资产包”下发。
- **单包（更省心但极大）**：使用 `DeepSeek.OCR2.Bundled`，把全部离线资产打进一个 nupkg（通常只建议私有源）。

本仓库的 `dotnet/src/DeepSeek.OCR2/Bundled/*` 为 **生成型资产目录**（便携 Python / wheels / 模型快照），默认不提交到 Git：

- GitHub LFS 对单文件有 2GB 上限；DeepSeek-OCR-2 的 safetensors 权重文件可能大于 2GB，直接推送会失败。
- 生产/发布：通过 CI 或本地脚本生成 Bundled 资产，再打包发布到私有源。
- 开发/调试：只在本地生成即可，或直接用 `DeepSeek.OCR2` + Assets 包组合。

准备 Bundled 资产（会下载大量内容）：

```powershell
pwsh .\dotnet\bundle\prepare-bundled-assets.ps1 -TorchPreset cpu -ModelId deepseek-ai/DeepSeek-OCR-2
```

随后打包：

```powershell
pwsh .\dotnet\pack.ps1 -PackBundled
```

为什么会很慢：

- `DeepSeek.OCR2.Assets.Model` 会把整个模型快照（含 safetensors 权重）打进 nupkg，文件很大，打包时需要大量磁盘读写与压缩。
- `DeepSeek.OCR2.Assets.Python.win-x64` / `DeepSeek.OCR2.Assets.Wheels.win-x64` 会包含成千上万个文件/whl，NuGet 打包需要枚举、哈希并写入 zip，也会显著耗时（并可能出现 Windows 路径过长警告）。

本地开发如果只想快速产物验证（不追求最小包体），可以用快速打包模式：

```powershell
pwsh .\dotnet\pack.ps1 -PackBundled -FastPack
```

如果你只是在改 .NET 客户端代码、并不需要重新产出离线资产包，可以跳过 Assets 包（避免模型/便携 Python 打包耗时）：

```powershell
pwsh .\dotnet\pack.ps1 -SkipAssets
```

## 仓库目录结构

```text
DeepSeekOCR2.NET/
├─ dotnet/                          .NET 封装与打包工程
│  ├─ src/                          各 NuGet 包的项目
│  │  ├─ DeepSeek.OCR2/             PackageId=DeepSeek.OCR2.Core（客户端 + 本地服务引导）
│  │  ├─ DeepSeek.OCR2.Meta/        PackageId=DeepSeek.OCR2（meta 包：拉取 Core + 资产包）
│  │  ├─ DeepSeek.OCR2.Assets.*     离线资产包（Python / wheels / 模型快照）
│  │  ├─ DeepSeek.OCR2.Bundled/     单包离线分发（含全部资产，包体很大）
│  │  └─ DeepSeek.OCR2.Full.win-x64/ 历史等价包（已停更）
│  ├─ samples/                      示例项目
│  ├─ tests/                        测试（契约/Smoke）
│  ├─ bundle/                       生成 bundled 资产的脚本
│  ├─ pack.ps1                      本地打包脚本
│  ├─ publish-nuget.ps1             打包并发布到 nuget.org
│  └─ push.ps1                      仅推送 nupkg
├─ DeepSeek-OCR2-master/            上游代码快照（便于对照/实验）
├─ assets/                          README 图片/徽章
└─ README.md                        本文件
```

## 许可证与致谢

- 本仓库为 .NET 封装与打包工程；模型与论文归上游项目所有。
- 致谢与引用请参考上游 DeepSeek-OCR-2 仓库与论文。
