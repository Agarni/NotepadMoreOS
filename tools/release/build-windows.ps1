# Builds the Windows single-file executable and the Inno Setup installer.
# Usage: pwsh tools/release/build-windows.ps1
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

$version = (Select-String -Path "Directory.Build.props" -Pattern "<Version>([^<]+)</Version>").Matches[0].Groups[1].Value
$publishDir = Join-Path $root "artifacts/windows-win-x64"
$installer = Join-Path $root "artifacts/NotepadMoreOS-$version-windows-x64-setup.exe"
$zip = Join-Path $root "artifacts/NotepadMoreOS-$version-windows-x64.zip"

Write-Host "NotepadMoreOS $version - Windows x64"

dotnet publish src/NotepadMoreOS -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -o $publishDir

Get-ChildItem $publishDir -Filter *.pdb | Remove-Item -Force

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    if (Test-Path $installer) { Remove-Item $installer -Force }
    & $iscc "tools\release\setup.iss" "/DMyAppVersion=$version"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }
    Write-Host "Installer: $installer"
}
else {
    Write-Warning "Inno Setup 6 not found; creating a portable zip instead."
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $publishDir "NotepadMoreOS.exe") -DestinationPath $zip
    Write-Host "Portable: $zip"
}
