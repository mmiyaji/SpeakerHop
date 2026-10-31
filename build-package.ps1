param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "1.0.29.0",
    [string]$PackageName = "mmiyaji.SpeakerHop",
    [string]$Publisher = "CN=663C5D03-791A-4B2C-8509-629FA1CA125A",
    [string]$PublisherDisplayName = "mmiyaji"
)

$ErrorActionPreference = "Stop"
$parsedVersion = $null
if (-not [Version]::TryParse($Version, [ref]$parsedVersion) -or
    $parsedVersion.Revision -lt 0 -or
    @($parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build, $parsedVersion.Revision).Where({ $_ -lt 0 -or $_ -gt 65535 }).Count -ne 0) {
    throw "Version must contain four numeric parts from 0 through 65535 (for example, 1.0.23.0)."
}
if ($Platform -ne "x64") {
    throw "Only the x64 platform is currently supported."
}
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\SpeakerHop\SpeakerHop.csproj"
$sourceManifest = Join-Path $root "src\SpeakerHop\Package.appxmanifest"
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
    if (-not $msbuild -or -not (Test-Path $msbuild)) {
        throw "MSBuild.exe was not found. Install Visual Studio with Windows app packaging tools."
    }
}

$packageRoot = Join-Path $root "artifacts\msix-layout"
$publishRoot = Join-Path $root "artifacts\publish-$Version"
$packageOut = Join-Path $root "artifacts\SpeakerHop_${Version}_${Platform}.msix"

$resolvedPackageRoot = [IO.Path]::GetFullPath($packageRoot)
if ($resolvedPackageRoot -ne [IO.Path]::GetFullPath((Join-Path $root 'artifacts\msix-layout'))) {
    throw 'Unexpected package staging path.'
}
Remove-Item -LiteralPath $resolvedPackageRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $packageOut) | Out-Null

$resolvedPublishRoot = [IO.Path]::GetFullPath($publishRoot)
if (-not $resolvedPublishRoot.StartsWith([IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unexpected publish path.'
}
Remove-Item -LiteralPath $resolvedPublishRoot -Recurse -Force -ErrorAction SilentlyContinue
& $msbuild $project /restore /t:Publish /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:WindowsAppSDKSelfContained=false /p:PublishDir="$publishRoot\" /p:Version=$Version /p:AssemblyVersion=$Version /p:FileVersion=$Version /p:InformationalVersion=$Version /m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# WinUI's generated PRI/XBF are build outputs, but are not automatically
# included by the .NET Publish target in this project.
$buildRoot = Join-Path $root "src\SpeakerHop\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\win-x64"
foreach ($resource in @('SpeakerHop.pri', 'App.xbf')) {
    $resourcePath = Join-Path $buildRoot $resource
    if (-not (Test-Path -LiteralPath $resourcePath)) {
        throw "Required WinUI resource is missing: $resourcePath"
    }
    Copy-Item -LiteralPath $resourcePath -Destination $publishRoot -Force
}

$windowIcon = Join-Path $publishRoot "Assets\App.ico"
if (-not (Test-Path (Join-Path $publishRoot 'coreclr.dll'))) {
    throw 'Self-contained .NET runtime is missing from publish output.'
}
if (-not (Test-Path $windowIcon)) {
    throw "Publish validation failed: Assets\App.ico is missing from $publishRoot"
}
$publishedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $publishRoot "SpeakerHop.exe")).ProductVersion
if (-not $publishedVersion -or -not $publishedVersion.StartsWith($Version)) {
    throw "Published app version '$publishedVersion' does not match requested version '$Version'."
}

Get-ChildItem $publishRoot -Force | Where-Object { -not $_.PSIsContainer -or $_.Name -ne "publish" } | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $packageRoot $_.Name) -Recurse -Force
}
Get-ChildItem $packageRoot -Recurse -Filter *.pdb | Remove-Item -Force
Remove-Item (Join-Path $packageRoot "Package.appxmanifest") -Force -ErrorAction SilentlyContinue

$appPri = Join-Path $packageRoot "SpeakerHop.pri"
if (Test-Path $appPri) {
    Move-Item $appPri (Join-Path $packageRoot "resources.pri") -Force
}

# Package.appxmanifest is the single manifest source. Only deployment-specific
# attributes are replaced here; XML APIs preserve escaping for user input.
[xml]$manifest = Get-Content -Raw $sourceManifest
$ns = New-Object Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace("f", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$ns.AddNamespace("desktop", "http://schemas.microsoft.com/appx/manifest/desktop/windows10")
$identity = $manifest.SelectSingleNode("/f:Package/f:Identity", $ns)
$identity.SetAttribute("Name", $PackageName)
$identity.SetAttribute("Publisher", $Publisher)
$identity.SetAttribute("Version", $Version)
$identity.SetAttribute("ProcessorArchitecture", $Platform)
$manifest.SelectSingleNode("/f:Package/f:Properties/f:PublisherDisplayName", $ns).InnerText = $PublisherDisplayName
$application = $manifest.SelectSingleNode("/f:Package/f:Applications/f:Application", $ns)
$application.SetAttribute("Executable", "SpeakerHop.exe")
$application.SetAttribute("EntryPoint", "Windows.FullTrustApplication")
$startupExtension = $manifest.SelectSingleNode("//desktop:Extension[@Category='windows.startupTask']", $ns)
$startupExtension.SetAttribute("Executable", "SpeakerHop.exe")
$startupExtension.SetAttribute("EntryPoint", "Windows.FullTrustApplication")
$manifestPath = Join-Path $packageRoot "AppxManifest.xml"
$xmlSettings = New-Object Xml.XmlWriterSettings
$xmlSettings.Encoding = New-Object Text.UTF8Encoding($false)
$xmlSettings.Indent = $true
$writer = [Xml.XmlWriter]::Create($manifestPath, $xmlSettings)
try { $manifest.Save($writer) } finally { $writer.Dispose() }

$makeAppx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*\x64\makeappx.exe" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) {
    throw "makeappx.exe was not found. Install Windows 10/11 SDK."
}

Remove-Item $packageOut -Force -ErrorAction SilentlyContinue
& $makeAppx.FullName pack /d $packageRoot /p $packageOut /o
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "MSIX created: $packageOut"
