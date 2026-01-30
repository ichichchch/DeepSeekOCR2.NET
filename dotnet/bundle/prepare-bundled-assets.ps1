param(
  [string]$PythonVersion = "3.10.11",
  [ValidateSet("win-x64")]
  [string]$Rid = "win-x64",
  [ValidateSet("cpu","cu118")]
  [string]$TorchPreset = "cpu",
  [string]$ModelId = "deepseek-ai/DeepSeek-OCR-2",
  [string]$OutputDir = "src\\DeepSeek.OCR2\\Bundled",
  [switch]$SkipTorch,
  [switch]$SkipModel
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$out = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDir))
New-Item -ItemType Directory -Force -Path $out | Out-Null

$pythonDir = Join-Path $out ("python\\$Rid\\$PythonVersion")
$wheelsDir = Join-Path $out ("wheels\\$Rid")
$modelDir = Join-Path $out ("models\\DeepSeek-OCR-2")

New-Item -ItemType Directory -Force -Path $pythonDir, $wheelsDir, $modelDir | Out-Null

$zipName = "python-$PythonVersion-embed-amd64.zip"
$zipPath = Join-Path $pythonDir $zipName
$pythonExe = Join-Path $pythonDir "python.exe"

if (!(Test-Path $pythonExe)) {
  if (!(Test-Path $zipPath)) {
    $url = "https://www.python.org/ftp/python/$PythonVersion/$zipName"
    Write-Host "Downloading Python embeddable: $url"
    Invoke-WebRequest -Uri $url -OutFile $zipPath
  }
  Write-Host "Extracting Python to: $pythonDir"
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $pythonDir, $true)
}

$pth = Get-ChildItem -Path $pythonDir -Filter "python*._pth" | Select-Object -First 1
if ($pth) {
  $lines = Get-Content $pth.FullName
  if ($lines -notcontains "Lib\site-packages") { $lines = @("Lib\site-packages") + $lines }
  $lines = $lines | ForEach-Object { if ($_ -eq "#import site") { "import site" } else { $_ } }
  if ($lines -notcontains "import site") { $lines += "import site" }
  Set-Content -Path $pth.FullName -Value ($lines -join "`r`n") -Encoding UTF8
}

$needPip = (-not $SkipTorch) -or (-not $SkipModel)
if ($needPip) {
  Write-Host "Bootstrapping pip for embeddable Python"
  $getPip = Join-Path $pythonDir "get-pip.py"
  if (!(Test-Path $getPip)) {
    Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile $getPip
  }

  & $pythonExe $getPip "--disable-pip-version-check" "--no-warn-script-location"
}

if (-not $SkipTorch) {
  Write-Host "Downloading wheels into: $wheelsDir"
  $indexUrl = if ($TorchPreset -eq "cu118") { "https://download.pytorch.org/whl/cu118" } else { "https://download.pytorch.org/whl/cpu" }
  & $pythonExe -m pip download torch==2.6.0 torchvision==0.21.0 torchaudio==2.6.0 --dest $wheelsDir --index-url $indexUrl
  & $pythonExe -m pip download -r (Join-Path $repoRoot "src\\DeepSeek.OCR2\\Python\\requirements_runtime.txt") --dest $wheelsDir
}

if (-not $SkipModel) {
  Write-Host "Downloading model snapshot into: $modelDir"
  $hfTarget = Join-Path $out ("_temp\\hf_hub")
  if (Test-Path $hfTarget) { Remove-Item -Recurse -Force $hfTarget }
  New-Item -ItemType Directory -Force -Path $hfTarget | Out-Null

  & $pythonExe -m pip install --upgrade huggingface_hub --target $hfTarget

  $oldPyPath = $env:PYTHONPATH
  if ([string]::IsNullOrWhiteSpace($oldPyPath)) { $env:PYTHONPATH = $hfTarget } else { $env:PYTHONPATH = "$hfTarget;$oldPyPath" }

  try {
    & $pythonExe (Join-Path $PSScriptRoot "snapshot_hf_model.py") --model $ModelId --out $modelDir
  }
  finally {
    $env:PYTHONPATH = $oldPyPath
    Remove-Item -Recurse -Force $hfTarget
  }
}

Write-Host "Done. Bundled assets root: $out"
