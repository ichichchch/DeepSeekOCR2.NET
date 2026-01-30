param(
  [string]$ApiKey = $env:NUGET_API_KEY,
  [string]$Source = "https://api.nuget.org/v3/index.json",
  [string]$PackageGlob = ".\\artifacts\\DeepSeek.OCR2*.nupkg",
  [string]$SymbolsGlob = ".\\artifacts\\DeepSeek.OCR2*.snupkg",
  [bool]$IncludeBundled = $true,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot
try {

  if (-not $DryRun -and [string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "ApiKey is required. Pass -ApiKey or set NUGET_API_KEY."
  }

  $packages = Get-ChildItem -Path $PackageGlob -ErrorAction SilentlyContinue | Where-Object {
    ($IncludeBundled -or $_.Name -notlike "DeepSeek.OCR2.Bundled.*") -and
    $_.Name -notlike "DeepSeek.OCR2.Full.*"
  }

  foreach ($pkg in $packages) {
    if ($DryRun) {
      Write-Output ("PUSH " + $pkg.Name)
    }
    else {
      dotnet nuget push $pkg.FullName --api-key $ApiKey --source $Source --skip-duplicate
      if ($LASTEXITCODE -ne 0) {
        throw ("dotnet nuget push failed: " + $pkg.Name)
      }
    }
  }

  $snupkgs = Get-ChildItem -Path $SymbolsGlob -ErrorAction SilentlyContinue | Where-Object {
    ($IncludeBundled -or $_.Name -notlike "DeepSeek.OCR2.Bundled.*") -and
    $_.Name -notlike "DeepSeek.OCR2.Full.*"
  }
  foreach ($pkg in $snupkgs) {
    if ($DryRun) {
      Write-Output ("PUSH " + $pkg.Name)
    }
    else {
      dotnet nuget push $pkg.FullName --api-key $ApiKey --source $Source --skip-duplicate
      if ($LASTEXITCODE -ne 0) {
        throw ("dotnet nuget push failed: " + $pkg.Name)
      }
    }
  }

}
finally {
  Pop-Location
}
