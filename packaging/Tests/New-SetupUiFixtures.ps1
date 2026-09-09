param([Parameter(Mandatory = $true)][string]$Version)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$output = Join-Path $root ('artifacts\validation\framework-setup\ui-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[void][IO.Directory]::CreateDirectory($output)
$cases = @(
    @{ Name='missing'; Release=0; Version='未检测到' },
    @{ Name='satisfied'; Release=461814; Version='4.7.03062' }
)
foreach ($case in $cases) {
    [xml]$document = [IO.File]::ReadAllText((Join-Path $root 'packaging\Setup.wxs'))
    $bundle = $document.DocumentElement.Bundle
    $bundle.SetAttribute('Name', 'EBAssistant UI fixture ' + $case.Name)
    $bundle.SetAttribute('UpgradeCode', [guid]::NewGuid().ToString())
    $theme = [IO.File]::ReadAllText((Join-Path $root 'packaging\SetupTheme.xml')).Replace('EBAssistant 安装', 'EBAssistant 安装测试 - ' + $case.Name).Replace('EBAssistant [WixBundleVersion]', '模拟环境：' + $case.Name)
    $themePath = Join-Path $output ($case.Name + '-theme.xml')
    [IO.File]::WriteAllText($themePath, $theme, [Text.UTF8Encoding]::new($false))
    $bundle.BootstrapperApplication.WixStandardBootstrapperApplication.SetAttribute('ThemeFile', $themePath)
    foreach ($node in @($bundle.ChildNodes)) {
        if ($node.LocalName -eq 'RegistrySearch') { [void]$bundle.RemoveChild($node) }
    }
    foreach ($variable in $bundle.Variable) {
        if ($variable.Name -eq 'NetFxRelease') { $variable.SetAttribute('Value', [string]$case.Release) }
        if ($variable.Name -eq 'NetFxVersion') { $variable.SetAttribute('Value', $case.Version) }
    }
    foreach ($package in $bundle.Chain.ChildNodes) {
        # Fixture executables cannot launch a Framework installer or install the MSI.
        $package.SetAttribute('InstallCondition', '0')
        $package.SetAttribute('Compressed', 'no')
        if ($package.LocalName -eq 'ExePackage') {
            $package.SetAttribute('SourceFile', (Join-Path $env:WINDIR 'System32\where.exe'))
            $package.SetAttribute('Name', $package.Id + '-fixture.exe')
            $package.SetAttribute('CacheId', $case.Name + '-' + $package.Id + '-fixture')
            $package.SetAttribute('InstallArguments', '/?')
        }
    }
    $path = Join-Path $output ($case.Name + '.wxs')
    $document.Save($path)
    & dotnet wix build $path -ext WixToolset.Bal.wixext -ext WixToolset.Util.wixext -arch x86 -d "Version=$Version" -d "SourceDir=$root\packaging" -d "PrerequisiteDir=$root\artifacts\installer\prerequisites" -d "MsiPath=$root\artifacts\installer\EBAssistant-$Version-x86.msi" -o (Join-Path $output ($case.Name + '.exe'))
    if ($LASTEXITCODE -ne 0) { throw "Fixture build failed: $($case.Name)" }
}
Write-Host "UI-only fixtures created: $output"
