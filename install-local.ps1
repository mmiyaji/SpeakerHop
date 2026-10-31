param(
    [string]$PackageDirectory = (Join-Path $PSScriptRoot 'package'),
    [string]$InstallDirectory,
    [switch]$SkipLaunch
)

$ErrorActionPreference = 'Stop'

function Get-NormalizedPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetPathRoot($full)
    if ($full.Length -gt $root.Length) { return $full.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) }
    return $full
}

function Test-PathContains([string]$Parent, [string]$Child) {
    $prefix = $Parent.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    return $Child.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Get-InstalledPackage {
    return Get-AppxPackage -Name 'mmiyaji.SpeakerHop' | Select-Object -First 1
}

$source = Get-NormalizedPath $PackageDirectory
$manifestPath = Join-Path $source 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Package manifest not found: $manifestPath" }
try { [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw } catch { throw "Package manifest is invalid XML: $($_.Exception.Message)" }
$identity = $manifest.Package.Identity
if (-not $identity -or $identity.Name -ne 'mmiyaji.SpeakerHop') { throw 'This installer only accepts the SpeakerHop package.' }
$version = $null
if (-not [Version]::TryParse([string]$identity.Version, [ref]$version) -or $version.Revision -lt 0) { throw 'Package manifest has an invalid version.' }

$requiredFiles = @('SpeakerHop.exe', 'SpeakerHop.dll', 'SpeakerHop.Core.dll', 'SpeakerHop.deps.json', 'SpeakerHop.runtimeconfig.json', 'resources.pri', 'App.xbf', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll')
foreach ($name in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Required package file is missing: $name" }
}
if (-not (Get-ChildItem -LiteralPath $source -Filter '*.dll' -File | Select-Object -First 1)) { throw 'Package contains no runtime DLLs.' }

# InstallDirectory is the preferred package directory. Existing directories are
# never reused; a unique sibling is selected for each update/install.
$requestedDestination = if ($InstallDirectory) {
    Get-NormalizedPath $InstallDirectory
} else {
    Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "SpeakerHopDeployment\$version"
}
$requestedDestination = Get-NormalizedPath $requestedDestination
if ($source -eq $requestedDestination -or (Test-PathContains $source $requestedDestination) -or (Test-PathContains $requestedDestination $source)) {
    throw 'Package source and install destination must be separate, non-nested directories.'
}
$destination = $requestedDestination
if (Test-Path -LiteralPath $destination) {
    $parent = Split-Path -Parent $destination
    $leaf = Split-Path -Leaf $destination
    $destination = Join-Path $parent ($leaf + '.' + [Guid]::NewGuid().ToString('N'))
}
$destination = Get-NormalizedPath $destination
$destinationParent = Get-NormalizedPath (Split-Path -Parent $destination)
if (-not (Test-PathContains $destinationParent $destination) -or (Test-PathContains $source $destination) -or (Test-PathContains $destination $source)) {
    throw 'Unsafe installation path.'
}
if (Test-Path -LiteralPath $destination) { throw "Fresh installation path already exists: $destination" }

# Copy and validate before disturbing the running app or its registration.
New-Item -ItemType Directory -Path $destination -Force | Out-Null
try {
    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
    }
    $copiedManifestPath = Join-Path $destination 'AppxManifest.xml'
    [xml]$copiedManifest = Get-Content -LiteralPath $copiedManifestPath -Raw
    if ($copiedManifest.Package.Identity.Name -ne 'mmiyaji.SpeakerHop' -or [Version]$copiedManifest.Package.Identity.Version -ne $version) {
        throw 'Staged package manifest identity or version changed during copy.'
    }
    foreach ($name in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $destination $name) -PathType Leaf)) { throw "Staged package validation failed: $name is missing." }
    }
} catch {
    if ((Test-Path -LiteralPath $destination) -and (Test-PathContains $destinationParent $destination)) { Remove-Item -LiteralPath $destination -Recurse -Force }
    throw
}

$previousPackage = Get-InstalledPackage
$previousManifest = $null
$previousVersion = $null
if ($previousPackage -and $previousPackage.InstallLocation) {
    $previousManifest = Join-Path (Get-NormalizedPath $previousPackage.InstallLocation) 'AppxManifest.xml'
    if (-not (Test-Path -LiteralPath $previousManifest -PathType Leaf)) { $previousManifest = $null }
}
$sameVersionDevelopmentUpdate = $previousPackage -and $previousPackage.InstallLocation -and $previousPackage.IsDevelopmentMode -and
    [Version]::TryParse([string]$previousPackage.Version, [ref]$previousVersion) -and
    $previousVersion -eq $version -and
    (Get-NormalizedPath $previousPackage.InstallLocation) -ne $destination
$wasRunning = [bool](Get-Process -Name SpeakerHop -ErrorAction SilentlyContinue | Select-Object -First 1)
$registered = $false
try {
    if ($wasRunning) { Get-Process -Name SpeakerHop -ErrorAction SilentlyContinue | Stop-Process -Force }
    if ($sameVersionDevelopmentUpdate) {
        Remove-AppxPackage -Package $previousPackage.PackageFullName -PreserveApplicationData
    }
    Add-AppxPackage -Register (Join-Path $destination 'AppxManifest.xml') -ForceApplicationShutdown
    $registered = $true
    $package = Get-InstalledPackage
    $registeredVersion = $null
    if (-not $package -or -not [Version]::TryParse([string]$package.Version, [ref]$registeredVersion) -or
        $registeredVersion -ne $version -or (Get-NormalizedPath $package.InstallLocation) -ne $destination) {
        throw 'Installed package version does not match the release.'
    }
} catch {
    $installError = $_
    # Registration may have changed even when Add-AppxPackage throws, so always
    # restore from the previously registered manifest when one was available.
    $restored = $false
    if (-not $previousPackage) {
        # An API failure may still have registered the new package. Never delete
        # the files of a package that remains registered after a failed install.
        $restored = -not (Get-InstalledPackage)
    }
    if ($previousManifest -and (Test-Path -LiteralPath $previousManifest -PathType Leaf)) {
        try {
            $activePackage = Get-InstalledPackage
            $activeVersion = $null
            $activeIsSameVersionElsewhere = $activePackage -and
                [Version]::TryParse([string]$activePackage.Version, [ref]$activeVersion) -and
                [Version]::TryParse([string]$previousPackage.Version, [ref]$previousVersion) -and
                $activeVersion -eq $previousVersion -and
                (Get-NormalizedPath $activePackage.InstallLocation) -ne (Get-NormalizedPath $previousPackage.InstallLocation)
            if ($activeIsSameVersionElsewhere -and $activePackage.IsDevelopmentMode) {
                Remove-AppxPackage -Package $activePackage.PackageFullName -PreserveApplicationData
            }
            Add-AppxPackage -Register $previousManifest -ForceApplicationShutdown -ForceUpdateFromAnyVersion
            $restoredPackage = Get-InstalledPackage
            $restoredVersion = $null
            $previousVersion = $null
            $restored = $restoredPackage -and
                [Version]::TryParse([string]$restoredPackage.Version, [ref]$restoredVersion) -and
                [Version]::TryParse([string]$previousPackage.Version, [ref]$previousVersion) -and
                $restoredVersion -eq $previousVersion -and
                (Get-NormalizedPath $restoredPackage.InstallLocation) -eq (Get-NormalizedPath $previousPackage.InstallLocation)
        } catch { $restored = $false }
    }
    if ($wasRunning -and $previousPackage -and $restored) {
        try {
            $shell = New-Object -ComObject Shell.Application
            $shell.ShellExecute('shell:AppsFolder\' + $previousPackage.PackageFamilyName + '!App')
        } catch { Write-Warning "Could not restart the previous SpeakerHop app: $($_.Exception.Message)" }
    }
    if ($restored -and (Test-Path -LiteralPath $destination) -and (Test-PathContains $destinationParent $destination)) {
        Remove-Item -LiteralPath $destination -Recurse -Force
    }
    if (-not $restored) {
        Write-Warning "The previous registration could not be restored. The staged package was preserved at $destination."
    }
    throw $installError
}

Write-Output "Installed SpeakerHop $version at $destination"
if (-not $SkipLaunch) {
    $shell = New-Object -ComObject Shell.Application
    $shell.ShellExecute('shell:AppsFolder\' + $package.PackageFamilyName + '!App')
}
