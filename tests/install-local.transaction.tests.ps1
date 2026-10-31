param([switch]$InChildProcess)

if (-not $InChildProcess) {
    $hostExecutable = (Get-Process -Id $PID).Path
    & $hostExecutable -NoProfile -File $PSCommandPath -InChildProcess
    if ($LASTEXITCODE -ne 0) { throw "Installer transaction test child failed with exit code $LASTEXITCODE." }
    return
}

$ErrorActionPreference = 'Stop'
$installer = Join-Path (Split-Path -Parent $PSScriptRoot) 'install-local.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('SpeakerHopInstallerTest-' + [Guid]::NewGuid().ToString('N'))
$global:mockPackage = $null
$global:failNewRegistration = $false
$global:failRegistrationLeaf = $null
$global:removeCalls = @()
$global:running = $false
$global:stopped = $false
$global:launches = 0

function New-TestPackage([string]$Path, [string]$Version) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    @"
<Package><Identity Name="mmiyaji.SpeakerHop" Version="$Version" /></Package>
"@ | Set-Content -LiteralPath (Join-Path $Path 'AppxManifest.xml') -Encoding utf8
    foreach ($file in @('SpeakerHop.exe', 'SpeakerHop.dll', 'SpeakerHop.Core.dll', 'SpeakerHop.deps.json', 'SpeakerHop.runtimeconfig.json', 'resources.pri', 'App.xbf', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll')) {
        Set-Content -LiteralPath (Join-Path $Path $file) -Value 'fixture'
    }
}

function Get-AppxPackage { param($Name) $global:mockPackage }
function global:Get-Process { param($Name, $ErrorAction) if ($global:running) { [pscustomobject]@{ ProcessName = $Name } } }
function global:Stop-Process { param([switch]$Force, $ErrorAction) $global:stopped = $true; $global:running = $false }
function Add-AppxPackage {
    param($Register, [switch]$ForceApplicationShutdown, [switch]$ForceUpdateFromAnyVersion)
    [xml]$m = Get-Content -LiteralPath $Register -Raw
    $v = [string]$m.Package.Identity.Version
    $path = Split-Path -Parent $Register
    $nextPackage = [pscustomobject]@{
        Version = $v
        InstallLocation = $path
        PackageFamilyName = 'mmiyaji.SpeakerHop_test'
        PackageFullName = "mmiyaji.SpeakerHop_$v`_x64__test"
        IsDevelopmentMode = $true
    }
    if ($global:mockPackage -and $global:mockPackage.Version -eq $v -and $global:mockPackage.InstallLocation -ne $path) { return }
    $global:mockPackage = $nextPackage
    if ($global:failNewRegistration -and (Split-Path -Leaf $path) -eq $global:failRegistrationLeaf) { throw 'simulated registration failure' }
}
function Remove-AppxPackage {
    param($Package, [switch]$PreserveApplicationData)
    $global:removeCalls += [pscustomobject]@{ Package = $Package; PreserveApplicationData = [bool]$PreserveApplicationData }
    $global:mockPackage = $null
}
function New-Object {
    param($ComObject)
    if ($ComObject -eq 'Shell.Application') {
        $shell = [pscustomobject]@{}
        $shell | Add-Member -MemberType ScriptMethod -Name ShellExecute -Value { param($target) $global:launches++ }
        return $shell
    }
    throw "Unexpected New-Object request: $ComObject"
}

try {
    $source = Join-Path $root 'source'
    $old = Join-Path $root 'old-install'
    $preferred = Join-Path $root 'preferred'
    New-TestPackage $source '2.0.0.0'
    New-TestPackage $old '1.0.0.0'
    Set-Content -LiteralPath (Join-Path $old 'user-data.txt') -Value 'keep'
    $global:mockPackage = [pscustomobject]@{ Version = '1.0.0.0'; InstallLocation = $old; PackageFamilyName = 'mmiyaji.SpeakerHop_test' }

    # Existing preferred paths are preserved and installation goes to a fresh sibling.
    New-Item -ItemType Directory -Path $preferred | Out-Null
    Set-Content -LiteralPath (Join-Path $preferred 'sentinel.txt') -Value 'keep'
    . $installer -PackageDirectory $source -InstallDirectory $preferred -SkipLaunch | Out-Null
    if ((Get-Content (Join-Path $preferred 'sentinel.txt') -Raw).Trim() -ne 'keep') { throw 'Preferred directory was modified.' }
    if ($global:mockPackage.Version -ne '2.0.0.0') { throw 'Successful registration did not select new version.' }
    if ((Get-Content (Join-Path $old 'user-data.txt') -Raw).Trim() -ne 'keep') { throw 'Prior package content was modified.' }

    # Same-version development registrations are explicitly moved without removing app data.
    $sameVersionOld = Join-Path $root 'same-version-old'
    New-TestPackage $sameVersionOld '2.0.0.0'
    Set-Content -LiteralPath (Join-Path $sameVersionOld 'user-data.txt') -Value 'keep same-version data'
    $global:mockPackage = [pscustomobject]@{ Version = '2.0.0.0'; InstallLocation = $sameVersionOld; PackageFamilyName = 'mmiyaji.SpeakerHop_test'; PackageFullName = 'old-development-package'; IsDevelopmentMode = $true }
    $global:failNewRegistration = $true
    $removeStart = $global:removeCalls.Count
    . $installer -PackageDirectory $source -InstallDirectory (Join-Path $root 'same-version-new') -SkipLaunch | Out-Null
    if ($global:mockPackage.Version -ne '2.0.0.0' -or $global:mockPackage.InstallLocation -ne (Join-Path $root 'same-version-new')) { throw 'Same-version development package path was not updated.' }
    if ($global:removeCalls.Count -ne ($removeStart + 1) -or -not $global:removeCalls[-1].PreserveApplicationData) { throw 'Same-version update did not unregister with PreserveApplicationData.' }
    if ((Get-Content (Join-Path $sameVersionOld 'user-data.txt') -Raw).Trim() -ne 'keep same-version data') { throw 'Same-version update modified old package content.' }

    # Rollback of a same-version development move unregisters the new location and restores the old one.
    $global:mockPackage = [pscustomobject]@{ Version = '2.0.0.0'; InstallLocation = $sameVersionOld; PackageFamilyName = 'mmiyaji.SpeakerHop_test'; PackageFullName = 'old-development-package'; IsDevelopmentMode = $true }
    $global:failRegistrationLeaf = 'same-version-rollback'
    $removeStart = $global:removeCalls.Count
    $sameVersionFailure = $null
    try { . $installer -PackageDirectory $source -InstallDirectory (Join-Path $root 'same-version-rollback') -SkipLaunch } catch { $sameVersionFailure = $_.Exception.Message }
    if ($sameVersionFailure -notlike '*simulated registration failure*') { throw 'Expected same-version registration failure.' }
    if ($global:mockPackage.Version -ne '2.0.0.0' -or $global:mockPackage.InstallLocation -ne $sameVersionOld) { throw 'Same-version rollback did not restore the old path.' }
    if ($global:removeCalls.Count -ne ($removeStart + 2) -or @($global:removeCalls[$removeStart..($global:removeCalls.Count - 1)] | Where-Object { -not $_.PreserveApplicationData }).Count) { throw 'Same-version rollback did not preserve application data on both unregisters.' }
    $global:failRegistrationLeaf = 'failed-install'

    # A registration failure restores the old registration and restarts a running app.
    $global:mockPackage = [pscustomobject]@{ Version = '1.0.0.0'; InstallLocation = $old; PackageFamilyName = 'mmiyaji.SpeakerHop_test' }
    $global:failNewRegistration = $true
    $global:running = $true
    $global:stopped = $false
    if ((Get-Command Get-Process).CommandType -ne 'Function' -or -not (Get-Process -Name SpeakerHop -ErrorAction SilentlyContinue)) { throw 'Process mock did not activate.' }
    $registrationFailure = $null
    try { . $installer -PackageDirectory $source -InstallDirectory (Join-Path $root 'failed-install') -SkipLaunch | Out-Null; throw 'Expected registration failure.' } catch {
        if ($_.Exception.Message -eq 'Expected registration failure.') { throw }
        $registrationFailure = $_.Exception.Message
    }
    if ($registrationFailure -notlike '*simulated registration failure*') { throw "Unexpected failure: $registrationFailure" }
    if ($global:mockPackage.Version -ne '1.0.0.0') { throw 'Old registration was not restored.' }
    if (-not $global:stopped) { throw "Running app was not stopped for registration (running=$global:running, processCommand=$((Get-Command Get-Process).CommandType), stopCommand=$((Get-Command Stop-Process).CommandType))." }
    if ($global:launches -lt 1) { throw 'Previously running app was not restarted.' }
    if ((Get-Content (Join-Path $old 'user-data.txt') -Raw).Trim() -ne 'keep') { throw 'Rollback modified prior package content.' }

    # Required files and source nesting are rejected before process shutdown.
    Remove-Item -LiteralPath (Join-Path $source 'resources.pri') -Force
    $global:stopped = $false
    $missingFileMessage = $null
    try { . $installer -PackageDirectory $source -InstallDirectory (Join-Path $root 'bad-package') -SkipLaunch } catch { $missingFileMessage = $_.Exception.Message }
    if ($missingFileMessage -notlike '*Required package file is missing: resources.pri*') { throw 'Missing package file was not rejected explicitly.' }
    if ($global:stopped) { throw 'Installer stopped the app before source validation.' }
    Set-Content -LiteralPath (Join-Path $source 'resources.pri') -Value 'fixture'
    $nestedMessage = $null
    try { . $installer -PackageDirectory $source -InstallDirectory (Join-Path $source 'nested') -SkipLaunch } catch { $nestedMessage = $_.Exception.Message }
    if (-not $nestedMessage -or $nestedMessage -notlike '*must be separate, non-nested*') { throw 'Nested install path was not rejected explicitly.' }

    # A first installation may register successfully before the deployment API
    # reports a failure. Keep those files if no prior registration can be restored.
    $global:mockPackage = $null
    $global:running = $false
    $global:failRegistrationLeaf = 'partial-first-install'
    $partialPath = Join-Path $root 'partial-first-install'
    $partialMessage = $null
    try { . $installer -PackageDirectory $source -InstallDirectory $partialPath -SkipLaunch 3>$null } catch { $partialMessage = $_.Exception.Message }
    if ($partialMessage -notlike '*simulated registration failure*') { throw 'Expected partial first-install failure.' }
    if (-not (Test-Path -LiteralPath (Join-Path $partialPath 'AppxManifest.xml'))) { throw 'Files were deleted while the partial registration was still active.' }
    if ($global:mockPackage.InstallLocation -ne $partialPath) { throw 'Expected the partial registration to remain associated with preserved files.' }

    'Installer transaction mocks: OK'
} finally {
    $resolvedRoot = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing test cleanup outside the temp directory.' }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
