param(
  [Parameter(Mandatory = $true)]
  [string]$ApiKey,
  [string]$Source = "https://api.nuget.org/v3/index.json",
  [string]$PackageGlob = ".\\artifacts\\DeepSeek.OCR2*.nupkg",
  [bool]$IncludeBundled = $true
)

$packages = Get-ChildItem -Path $PackageGlob -ErrorAction SilentlyContinue | Where-Object {
  ($IncludeBundled -or $_.Name -notlike "DeepSeek.OCR2.Bundled.*") -and
  $_.Name -notlike "DeepSeek.OCR2.Full.*"
}

foreach ($pkg in $packages) {
  dotnet nuget push $pkg.FullName --api-key $ApiKey --source $Source --skip-duplicate
}
