param(
  [string]$Configuration = "Release",
  [string]$Output = "artifacts",
  [switch]$PrepareBundledAssets,
  [switch]$PackBundled,
  [switch]$FastPack,
  [switch]$SkipAssets,
  [switch]$SkipAssetsPython,
  [switch]$SkipAssetsWheels,
  [switch]$SkipAssetsModel,
  [switch]$SkipMeta,
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

$packCommonArgs = @("-c", $Configuration, "-o", ".\\$Output")
$packFastArgs = @()
if ($FastPack) {
  $packFastArgs = @("-p:NoPackageAnalysis=true", "-p:PackageCompressionLevel=NoCompression")
}

dotnet pack .\src\DeepSeek.OCR2\DeepSeek.OCR2.csproj @packCommonArgs @packFastArgs

if ($PackBundled) {
  dotnet pack .\src\DeepSeek.OCR2.Bundled\DeepSeek.OCR2.Bundled.csproj @packCommonArgs @packFastArgs
}

$skipAssetsPython = $SkipAssets -or $SkipAssetsPython
$skipAssetsWheels = $SkipAssets -or $SkipAssetsWheels
$skipAssetsModel = $SkipAssets -or $SkipAssetsModel

if (-not $skipAssetsPython) {
  dotnet pack .\src\DeepSeek.OCR2.Assets.Python.win-x64\DeepSeek.OCR2.Assets.Python.win-x64.csproj @packCommonArgs @packFastArgs
}

if (-not $skipAssetsWheels) {
  dotnet pack .\src\DeepSeek.OCR2.Assets.Wheels.win-x64\DeepSeek.OCR2.Assets.Wheels.win-x64.csproj @packCommonArgs @packFastArgs
}

if (-not $skipAssetsModel) {
  dotnet pack .\src\DeepSeek.OCR2.Assets.Model\DeepSeek.OCR2.Assets.Model.csproj @packCommonArgs @packFastArgs
}

if (-not $SkipMeta) {
  dotnet pack .\src\DeepSeek.OCR2.Meta\DeepSeek.OCR2.Meta.csproj @packCommonArgs @packFastArgs
}

}
finally {
  Pop-Location
}
