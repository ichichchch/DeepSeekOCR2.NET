<!-- markdownlint-disable first-line-h1 -->

# DeepSeekOCR2.NET

DeepSeekOCR2.NET 是一个高性能 OCR 识别库，基于 **DeepSeek-OCR-2** 模型构建，提供简洁的 .NET API 接口。

- 模型来源（Hugging Face）：https://huggingface.co/deepseek-ai/DeepSeek-OCR-2
- 研究论文（PDF）：https://github.com/deepseek-ai/DeepSeek-OCR-2/blob/main/DeepSeek_OCR2_paper.pdf
- 详细使用文档：[dotnet/README.md](dotnet/README.md)

## 快速开始

### 1. 安装 NuGet 包

推荐使用 `DeepSeek.OCR2` 包（自动包含所需依赖）：

```bash
dotnet add package DeepSeek.OCR2
```

### 2. 基本使用

```csharp
using DeepSeek.OCR2;

// 识别图片
var result = await DeepSeekOcr2.RecognizeFileAsync(@"D:\test.jpg");
Console.WriteLine(result.Text);
```

### 3. 复用会话（性能更优）

```csharp
using DeepSeek.OCR2;

// 创建会话（可复用）
await using var session = await DeepSeekOcr2.CreateSessionAsync();

// 自定义提示词
var request = DeepSeekOcr2Request.FromFile(@"D:\test.jpg") with
{
    Prompt = "<image>\nFree OCR."
};

var result = await session.Client.RecognizeAsync(request);
Console.WriteLine(result.Text);
```

### 4. 支持的输入格式

- 图片文件：JPG, PNG, BMP 等常见格式
- PDF 文件：支持多页 PDF
- Base64 数据：可直接传入 Base64 编码的图像数据

## 高级用法

### 批量识别

```csharp
var files = new[] { "page1.jpg", "page2.jpg", "page3.jpg" };
await using var session = await DeepSeekOcr2.CreateSessionAsync();

foreach (var file in files)
{
    var result = await session.Client.RecognizeAsync(DeepSeekOcr2Request.FromFile(file));
    Console.WriteLine($"{file}: {result.Text}");
}
```

### 自定义配置

```csharp
var options = new DeepSeekOcr2LocalServerOptions
{
    // 设置 GPU 设备（如需要）
    // DeviceId = 0,
    
    // 其他配置选项
};

await using var session = await DeepSeekOcr2.CreateSessionAsync(options);
```

## 常见问题

### 首次运行很慢？

首次使用时会自动下载模型和依赖项，请耐心等待。后续运行将直接使用本地缓存。

### 支持哪些平台？

目前主要支持 Windows x64 平台。其他平台支持正在开发中。

### 如何离线使用？

首次运行后，所有依赖和模型都会缓存在本地，之后可以完全离线使用。

## 项目结构

```
DeepSeekOCR2.NET/
├─ dotnet/                    .NET 实现与示例
│  ├─ samples/                示例项目
│  └─ src/                    源代码
└─ README.md                  本文件
```

## 许可证与致谢

本项目基于 DeepSeek-OCR-2 模型构建。模型与论文版权归上游项目所有。
