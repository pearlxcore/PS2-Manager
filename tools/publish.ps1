# Builds the release: a self-contained, single-file "PS2 Manager.exe" (no .NET install needed)
# and a zip of it, in artifacts/. Run from anywhere: pwsh tools/publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'PS2Manager.App/PS2Manager.App.csproj'
$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$out = Join-Path $root "artifacts/PS2Manager-$version-win-x64"

Write-Host "Testing..."
dotnet test (Join-Path $root 'PS2Manager.Tests') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

Write-Host "Publishing $version..."
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish $proj -c Release -r win-x64 -o $out --nologo -v q `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

Copy-Item (Join-Path $root 'README.md') $out
# The bundled save-state converter is GPL-3.0; ship its license and notes.
$lic = New-Item -ItemType Directory (Join-Path $out 'licenses/pcsx2-stateconvert')
Get-ChildItem (Join-Path $root 'third_party/pcsx2-stateconvert') -Include LICENSE,*.txt,*.md -File -Recurse | Copy-Item -Destination $lic
$zip = "$out.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip

$exe = Get-Item (Join-Path $out 'PS2 Manager.exe')
Write-Host ("Done: {0} ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB))
Write-Host ("Zip:  {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
