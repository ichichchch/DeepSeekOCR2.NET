param(
  [string]$Version,
  [string]$Source = "https://api.nuget.org/v3/index.json",
  [string]$ApiKey = $env:NUGET_API_KEY,
  [switch]$UnlistInternal = $true,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot
try {

if ([string]::IsNullOrWhiteSpace($Version)) {
  $pkg = Get-ChildItem -Path ".\\artifacts\\DeepSeek.OCR2.*.nupkg" -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -match '^DeepSeek\.OCR2\.([0-9]+\.[0-9]+\.[0-9]+)\.nupkg$'
  } | Sort-Object Name -Descending | Select-Object -First 1
  if ($pkg) {
    $pkg.Name -match '^DeepSeek\.OCR2\.([0-9]+\.[0-9]+\.[0-9]+)\.nupkg$' | Out-Null
    $Version = $Matches[1]
  }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
  throw "Version is required. Pass -Version <x.y.z> or ensure artifacts\\DeepSeek.OCR2.<x.y.z>.nupkg exists."
}

if (-not $DryRun -and [string]::IsNullOrWhiteSpace($ApiKey)) {
  throw "ApiKey is required. Pass -ApiKey or set NUGET_API_KEY environment variable."
}

& .\pack.ps1 -Configuration Release -Output artifacts

$packages = Get-ChildItem -Path ".\\artifacts\\DeepSeek.OCR2*.nupkg" | Where-Object {
  $_.Name -like "*.$Version.nupkg" -and
  $_.Name -notlike "DeepSeek.OCR2.Bundled.*" -and
  $_.Name -notlike "DeepSeek.OCR2.Full.*"
}

foreach ($pkg in $packages) {
  if ($DryRun) {
    Write-Output ("PUSH " + $pkg.Name)
  }
  else {
    dotnet nuget push $pkg.FullName --api-key $ApiKey --source $Source --skip-duplicate
  }
}

$snupkgs = Get-ChildItem -Path ".\\artifacts\\DeepSeek.OCR2*.snupkg" -ErrorAction SilentlyContinue | Where-Object {
  $_.Name -like "*.$Version.snupkg"
}
foreach ($pkg in $snupkgs) {
  if ($DryRun) {
    Write-Output ("PUSH " + $pkg.Name)
  }
  else {
    dotnet nuget push $pkg.FullName --api-key $ApiKey --source $Source --skip-duplicate
  }
}

if ($UnlistInternal) {
  $ids = @(
    "DeepSeek.OCR2.Core",
    "DeepSeek.OCR2.Assets.Python.win-x64",
    "DeepSeek.OCR2.Assets.Wheels.win-x64",
    "DeepSeek.OCR2.Assets.Model.DeepSeekOCR2"
  )
  foreach ($id in $ids) {
    if ($DryRun) {
      Write-Output ("UNLIST " + $id + " " + $Version)
    }
    else {
      dotnet nuget delete $id $Version --api-key $ApiKey --source $Source --non-interactive
    }
  }
}

}
finally {
  Pop-Location
}
