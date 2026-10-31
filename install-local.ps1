param(
    [string]$PackageDirectory = (Join-Path $PSScriptRoot 'package'),
    [string]$InstallDirectory,
    [switch]$SkipLaunch
)

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($PackageDirectory)
$manifestPath = Join-Path $source 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Package manifest not found: $manifestPath"
}
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
if ($manifest.Package.Identity.Name -ne 'mmiyaji.SpeakerHop') {
    throw 'This installer only accepts the SpeakerHop package.'
}
$version = [Version]$manifest.Package.Identity.Version
$destination = if ($InstallDirectory) {
    [IO.Path]::GetFullPath($InstallDirectory)
} else {
    Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "SpeakerHopDeployment\$version"
}
if ([IO.Path]::GetFullPath($destination) -eq $source) {
    throw 'Run this installer from the extracted release, not the installed directory.'
}

# Keep the loose package outside the checkout or extracted download folder.
# Never uninstall the existing package: registration preserves application data.
Get-Process SpeakerHop -ErrorAction SilentlyContinue | Stop-Process
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
}
Add-AppxPackage -Register (Join-Path $destination 'AppxManifest.xml') -ForceApplicationShutdown
$package = Get-AppxPackage -Name 'mmiyaji.SpeakerHop'
if (-not $package -or $package.Version -ne $version) {
    throw 'Installed package version does not match the release.'
}
Write-Output "Installed SpeakerHop $version at $destination"
if (-not $SkipLaunch) {
    $shell = New-Object -ComObject Shell.Application
    $shell.ShellExecute('shell:AppsFolder\' + $package.PackageFamilyName + '!App')
}
