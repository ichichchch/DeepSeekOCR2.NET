param(
  [string]$Configuration = "Release",
  [string]$Output = "artifacts",
  [switch]$PrepareBundledAssets,
  [switch]$PackBundled,
  [string]$PythonVersion = "3.10.11",
  [ValidateSet("cpu","cu118")]
  [string]$TorchPreset = "cpu",
  [string]$ModelId = "deepseek-ai/DeepSeek-OCR-2",
  [switch]$SkipTorch,
  [switch]$SkipModel
)

Push-Location $PSScriptRoot
try {

if ($PrepareBundledAssets) {
  $bundleScript = Join-Path $PSScriptRoot "bundle\\prepare-bundled-assets.ps1"
  & $bundleScript -PythonVersion $PythonVersion -TorchPreset $TorchPreset -ModelId $ModelId -SkipTorch:$SkipTorch -SkipModel:$SkipModel
}

dotnet pack .\src\DeepSeek.OCR2\DeepSeek.OCR2.csproj -c $Configuration -o .\$Output

if ($PackBundled) {
  dotnet pack .\src\DeepSeek.OCR2.Bundled\DeepSeek.OCR2.Bundled.csproj -c $Configuration -o .\$Output
}

dotnet pack .\src\DeepSeek.OCR2.Assets.Python.win-x64\DeepSeek.OCR2.Assets.Python.win-x64.csproj -c $Configuration -o .\$Output
dotnet pack .\src\DeepSeek.OCR2.Assets.Wheels.win-x64\DeepSeek.OCR2.Assets.Wheels.win-x64.csproj -c $Configuration -o .\$Output
dotnet pack .\src\DeepSeek.OCR2.Assets.Model.DeepSeekOCR2\DeepSeek.OCR2.Assets.Model.DeepSeekOCR2.csproj -c $Configuration -o .\$Output
dotnet pack .\src\DeepSeek.OCR2.Meta\DeepSeek.OCR2.Meta.csproj -c $Configuration -o .\$Output

}
finally {
  Pop-Location
}
