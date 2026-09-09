param(
    [string]$Version = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "EBAssistant.csproj"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionNode = Select-Xml -Path $projectPath -XPath "/Project/PropertyGroup/Version" | Select-Object -First 1
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.Node.InnerText)) {
        throw "Version was not provided and $projectPath does not define <Version>."
    }
    $Version = $versionNode.Node.InnerText.Trim()
}

$artifacts = Join-Path $root "artifacts\installer"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$stage = Join-Path $artifacts "work\$stamp"
$appStage = Join-Path $stage "app"
$wixDir = Join-Path $stage "wix"
$output = Join-Path $artifacts "EBAssistant-$Version-x86.msi"

function Get-MSBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw "vswhere.exe was not found. Install Visual Studio Build Tools with MSBuild."
    }

    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
    if (-not $msbuild) {
        throw "MSBuild was not found. Install Visual Studio Build Tools with MSBuild."
    }

    return $msbuild
}

function Convert-ToWixId([string]$Value) {
    $builder = New-Object System.Text.StringBuilder
    foreach ($ch in $Value.ToCharArray()) {
        if (($ch -ge 'A' -and $ch -le 'Z') -or ($ch -ge 'a' -and $ch -le 'z') -or ($ch -ge '0' -and $ch -le '9') -or $ch -eq '_') {
            [void]$builder.Append($ch)
        } else {
            [void]$builder.Append('_')
        }
    }

    $id = $builder.ToString().Trim('_')
    if ([string]::IsNullOrWhiteSpace($id)) {
        $id = "Id"
    }
    if ($id[0] -ge '0' -and $id[0] -le '9') {
        $id = "Id_$id"
    }
    return $id
}

function Get-RelativePath([string]$FromDirectory, [string]$ToPath) {
    $fromFull = [System.IO.Path]::GetFullPath($FromDirectory).TrimEnd('\') + '\'
    $toFull = [System.IO.Path]::GetFullPath($ToPath)
    $fromUri = New-Object System.Uri($fromFull)
    $toUri = New-Object System.Uri($toFull)
    return [System.Uri]::UnescapeDataString($fromUri.MakeRelativeUri($toUri).ToString()).Replace('/', '\')
}

function Escape-Xml([string]$Value) {
    return [System.Security.SecurityElement]::Escape($Value)
}

function New-WixSource([string]$SourceRoot, [string]$DestinationPath) {
    $directories = Get-ChildItem -LiteralPath $SourceRoot -Directory -Recurse | Sort-Object FullName
    $files = Get-ChildItem -LiteralPath $SourceRoot -File -Recurse | Sort-Object FullName
    $dirIds = @{}
    $sourceFull = [System.IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
    $dirIds[$sourceFull] = "INSTALLFOLDER"

    foreach ($dir in $directories) {
        $relative = Get-RelativePath $sourceFull $dir.FullName
        $dirIds[[System.IO.Path]::GetFullPath($dir.FullName)] = "DIR_" + (Convert-ToWixId $relative)
    }

    $directoryLines = New-Object System.Collections.Generic.List[string]
    foreach ($dir in $directories) {
        $parentPath = [System.IO.Path]::GetFullPath((Split-Path -Parent $dir.FullName))
        $id = $dirIds[[System.IO.Path]::GetFullPath($dir.FullName)]
        $parentId = $dirIds[$parentPath]
        $name = Escape-Xml $dir.Name
        $directoryLines.Add("    <DirectoryRef Id=`"$parentId`">")
        $directoryLines.Add("      <Directory Id=`"$id`" Name=`"$name`" />")
        $directoryLines.Add("    </DirectoryRef>")
    }

    $componentLines = New-Object System.Collections.Generic.List[string]
    $componentRefs = New-Object System.Collections.Generic.List[string]
    $index = 1
    foreach ($file in $files) {
        $relative = Get-RelativePath $sourceFull $file.FullName
        $dirPath = [System.IO.Path]::GetFullPath((Split-Path -Parent $file.FullName))
        $dirId = $dirIds[$dirPath]
        $baseId = Convert-ToWixId $relative
        $componentId = "CMP_{0:D4}_$baseId" -f $index
        $fileId = "FIL_{0:D4}_$baseId" -f $index
        $source = Escape-Xml $file.FullName

        $componentLines.Add("    <DirectoryRef Id=`"$dirId`">")
        $componentLines.Add("      <Component Id=`"$componentId`" Guid=`"*`">")
        $componentLines.Add("        <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />")
        $componentLines.Add("      </Component>")
        $componentLines.Add("    </DirectoryRef>")
        $componentRefs.Add("      <ComponentRef Id=`"$componentId`" />")
        $index++
    }

    $content = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
  <Package Name="EBAssistant" Manufacturer="EBAssistant" Version="$Version" UpgradeCode="7f8e7b4b-6d75-4f4c-9c96-7f43e4c2099c" Scope="perMachine" Codepage="65001">
    <MajorUpgrade DowngradeErrorMessage="A newer version of EBAssistant is already installed." />
    <MediaTemplate EmbedCab="yes" />
    <PropertyRef Id="WIX_IS_NETFRAMEWORK_462_OR_LATER_INSTALLED" />
    <Launch Condition="Installed OR WIX_IS_NETFRAMEWORK_462_OR_LATER_INSTALLED"
            Message="EBAssistant requires .NET Framework 4.6.2 or later. Run the Setup.exe installer to check and install prerequisites." />
    <ui:WixUI Id="WixUI_InstallDir" InstallDirectory="INSTALLFOLDER" />

    <StandardDirectory Id="ProgramFilesFolder">
      <Directory Id="INSTALLFOLDER" Name="EBAssistant" />
    </StandardDirectory>

    <Feature Id="MainFeature" Title="EBAssistant" Level="1">
      <ComponentGroupRef Id="AppComponents" />
      <ComponentRef Id="StartMenuShortcutComponent" />
    </Feature>
  </Package>

  <Fragment>
    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="EBAssistant" />
    </StandardDirectory>

    <DirectoryRef Id="ApplicationProgramsFolder">
      <Component Id="StartMenuShortcutComponent" Guid="*">
        <Shortcut Id="ApplicationStartMenuShortcut" Name="EBAssistant" Description="EBAssistant" Target="[INSTALLFOLDER]EBAssistant.exe" WorkingDirectory="INSTALLFOLDER" />
        <RemoveFolder Id="RemoveApplicationProgramsFolder" On="uninstall" />
        <RegistryValue Root="HKLM" Key="Software\EBAssistant" Name="StartMenuShortcut" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </DirectoryRef>
  </Fragment>

  <Fragment>
$($directoryLines -join [Environment]::NewLine)
  </Fragment>

  <Fragment>
$($componentLines -join [Environment]::NewLine)
  </Fragment>

  <Fragment>
    <ComponentGroup Id="AppComponents">
$($componentRefs -join [Environment]::NewLine)
    </ComponentGroup>
  </Fragment>
</Wix>
"@

    [System.IO.File]::WriteAllText($DestinationPath, $content, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Preparing installer artifacts..."
[System.IO.Directory]::CreateDirectory($artifacts) | Out-Null
if (Test-Path -LiteralPath $output) {
    [System.IO.File]::Delete($output)
}
[System.IO.Directory]::CreateDirectory($appStage) | Out-Null
[System.IO.Directory]::CreateDirectory($wixDir) | Out-Null

Write-Host "Restoring installer toolchain..."
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet wix extension add WixToolset.UI.wixext/4.0.6
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet wix extension add WixToolset.Netfx.wixext/4.0.6
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing self-contained WinForms app..."
dotnet publish (Join-Path $root "EBAssistant.csproj") `
    -c $Configuration `
    -r win-x86 `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -o $appStage
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$msbuild = Get-MSBuildPath

Write-Host "Building EB 2023 adapter..."
& $msbuild (Join-Path $root "Adapters\2023\EBAssistant.Adapter2023.csproj") /t:Build /p:Configuration=$Configuration /p:OutputPath="$(Join-Path $appStage 'Adapters\2023')\" /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building EB 2024 adapter..."
& $msbuild (Join-Path $root "Adapters\2024\EBAssistant.Adapter2024.csproj") /t:Build /p:Configuration=$Configuration /p:OutputPath="$(Join-Path $appStage 'Adapters\2024')\" /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building EB 2025 placeholder adapter..."
dotnet build (Join-Path $root "Adapters\2025\EBAssistant.Adapter2025.csproj") -c $Configuration -p:OutputPath="$(Join-Path $appStage 'Adapters\2025')\" --disable-build-servers
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$wxs = Join-Path $wixDir "EBAssistant.generated.wxs"
Write-Host "Generating WiX source..."
New-WixSource $appStage $wxs

Write-Host "Building MSI installer..."
dotnet wix build $wxs -ext WixToolset.UI.wixext -ext WixToolset.Netfx.wixext -o $output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installer created: $output"
& (Join-Path $PSScriptRoot 'package-setup.ps1') -MsiPath $output -Version $Version
exit $LASTEXITCODE
