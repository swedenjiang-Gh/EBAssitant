$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild was not found." }

dotnet build (Join-Path $root "EBAssistant.csproj")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $msbuild (Join-Path $root "Adapters\2023\EBAssistant.Adapter2023.csproj") /t:Build /p:Configuration=Debug /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $msbuild (Join-Path $root "Adapters\2024\EBAssistant.Adapter2024.csproj") /t:Build /p:Configuration=Debug /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build (Join-Path $root "Adapters\2025\EBAssistant.Adapter2025.csproj")
exit $LASTEXITCODE