param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "1.0.0.0",
    [string]$Publisher = "CN=SpeakerHop"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\SpeakerHop\SpeakerHop.csproj"
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    throw "MSBuild.exe was not found. Install Visual Studio Build Tools with Windows app packaging tools."
}

& $msbuild $project /restore /p:Configuration=$Configuration /p:Platform=$Platform /p:Version=$Version /p:AssemblyVersion=$Version /p:FileVersion=$Version /p:InformationalVersion=$Version /m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$tfm = "net8.0-windows10.0.19041.0"
$buildOutput = Join-Path $root "src\SpeakerHop\bin\$Platform\$Configuration\$tfm"
$packageRoot = Join-Path $root "artifacts\msix-layout"
$packageOut = Join-Path $root "artifacts\SpeakerHop_${Version}_${Platform}.msix"

Remove-Item $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $packageOut) | Out-Null

Get-ChildItem $buildOutput -Force | Where-Object { $_.Name -ne "publish" } | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $packageRoot $_.Name) -Recurse -Force
}

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap desktop rescap">
  <Identity Name="SpeakerHop" Publisher="$Publisher" Version="$Version" ProcessorArchitecture="$Platform" />
  <Properties>
    <DisplayName>SpeakerHop</DisplayName>
    <PublisherDisplayName>SpeakerHop</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26200.0" />
  </Dependencies>
  <Resources>
    <Resource Language="ja-jp" />
    <Resource Language="en-us" />
    <Resource Language="zh-cn" />
    <Resource Language="zh-tw" />
    <Resource Language="ko-kr" />
    <Resource Language="de-de" />
    <Resource Language="fr-fr" />
    <Resource Language="es-es" />
  </Resources>
  <Applications>
    <Application Id="App" Executable="SpeakerHop.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="SpeakerHop"
        Description="Quickly switch Windows output devices from the tray or shortcuts."
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" Square71x71Logo="Assets\SmallTile.png" Square310x310Logo="Assets\LargeTile.png" />
        <uap:SplashScreen Image="Assets\SplashScreen.png" BackgroundColor="transparent" />
      </uap:VisualElements>
      <Extensions>
        <desktop:Extension Category="windows.startupTask" Executable="SpeakerHop.exe" EntryPoint="Windows.FullTrustApplication">
          <desktop:StartupTask TaskId="SpeakerHopStartup" Enabled="false" DisplayName="SpeakerHop" />
        </desktop:Extension>
      </Extensions>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@
$manifest | Set-Content -Path (Join-Path $packageRoot "AppxManifest.xml") -Encoding UTF8

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
