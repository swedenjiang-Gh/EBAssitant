param(
    [Parameter(Mandatory = $true)][string]$MsiPath,
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$PrerequisiteDirectory = ''
)

$ErrorActionPreference = 'Stop'
# Use this host's module when PowerShell 7 launches Windows PowerShell 5.1.
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1') -ErrorAction Stop
if ([string]::IsNullOrWhiteSpace($PrerequisiteDirectory)) {
    $PrerequisiteDirectory = Join-Path $PSScriptRoot '..\artifacts\installer\prerequisites'
}
$payloads = @{
    'NDP462-KB3151800-x86-x64-AllOS-ENU.exe' = '8550E370DB2400AEDB4397E9958F12041DEF3BB63C03CC7625CE07E09F42303E'
}
foreach ($name in $payloads.Keys) {
    $path = Join-Path $PrerequisiteDirectory $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing Microsoft offline installer: $path. See docs/packaging.md for official download links."
    }
    $stream = [IO.File]::OpenRead($path)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha256.Dispose() }
    if ($hash -ne $payloads[$name]) {
        throw "Microsoft installer hash mismatch: $path"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )O=Microsoft Corporation(,|$)') {
        throw "Microsoft installer signature verification failed: $path"
    }
}

$msi = (Resolve-Path -LiteralPath $MsiPath).Path
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase($msi, 0)
$view = $database.OpenView('SELECT `Value` FROM `Property` WHERE `Property` = ''ProductVersion''')
try {
    $view.Execute()
    $msiVersion = $view.Fetch().StringData(1)
    if ($msiVersion -ne $Version) { throw "MSI version $msiVersion does not match setup version $Version" }
}
finally {
    $view.Close()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
}
$output = Join-Path (Split-Path -Parent $msi) "EBAssistant-$Version-x86-Setup.exe"
foreach ($extension in @('WixToolset.Bal.wixext', 'WixToolset.Util.wixext')) {
    dotnet wix extension add "$extension/4.0.6"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$wixArguments = @(
    'wix', 'build', (Join-Path $PSScriptRoot 'Setup.wxs'),
    '-ext', 'WixToolset.Bal.wixext', '-ext', 'WixToolset.Util.wixext',
    '-arch', 'x86', '-d', "Version=$Version", '-d', "SourceDir=$PSScriptRoot",
    '-d', "PrerequisiteDir=$((Resolve-Path -LiteralPath $PrerequisiteDirectory).Path)",
    '-d', "MsiPath=$msi", '-o', $output
)
& dotnet @wixArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Offline setup created: $output"
