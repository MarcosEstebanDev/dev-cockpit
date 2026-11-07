# Genera dist\DevCockpit.App.exe: un solo ejecutable que no necesita .NET instalado.
# Uso: .\scripts\publish.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"

Get-Process DevCockpit.App -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet publish (Join-Path $root "src\DevCockpit.App\DevCockpit.App.csproj") `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o $dist
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló" }
# Los paquetes nativos (Skia, HarfBuzz) copian ~100 MB de símbolos de depuración que no hacen falta.
Get-ChildItem $dist -Filter *.pdb | Remove-Item

$exe = Join-Path $dist "DevCockpit.App.exe"
"Listo: $exe ({0:N0} MB)" -f ((Get-Item $exe).Length / 1MB)
