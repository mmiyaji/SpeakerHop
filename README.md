# SpeakerHop

WinUI 3 desktop app for switching the Windows default audio output device from the tray icon or global shortcuts.

![SpeakerHop overview with actual app screenshot](docs/images/speakerhop-readme-overview.png)

SpeakerHop is a small Windows tray utility for people who frequently move audio between speakers, headphones, monitors, TVs, and virtual devices. Choose the output devices that should be part of the cycle, then switch between them from the tray icon or a global shortcut without opening Windows settings.

The README image uses an actual light-theme English SpeakerHop screenshot embedded in a generated presentation background. The Devices page lets you select which active render endpoints are included in the switching cycle. Disconnected or unused outputs can stay out of the rotation, while notifications confirm each successful switch.

## Features

- Left-navigation WinUI 3 settings window.
- Tray icon left-click cycles through selected output devices.
- Device settings page controls which render endpoints are included in the cycle.
- Global shortcuts can cycle, switch to a fixed device, change volume, toggle mute for the current default output device, or toggle mute for the Windows default capture endpoint.
- The shortcut editor lists every action with its key binding, enabled state, and relevant target or volume-step setting. Unassigned actions remain visible, and each action supports multiple bindings.

The existing **MuteToggle** action continues to mute the default output device. **MicrophoneMuteToggle** mutes the default capture endpoint for the Multimedia role; it does not change or use a separately assigned Communications default device. Adding the microphone action does not assign it a shortcut automatically.
- MSIX manifest and packaging script are included for Microsoft Store preparation.

## Default shortcuts

| Action | Shortcut |
| --- | --- |
| Cycle output device | Ctrl + Alt + Shift + F9 |
| Volume down | Ctrl + Alt + Shift + F10 |
| Volume up | Ctrl + Alt + Shift + F11 |

These defaults apply to new settings. Updates preserve saved shortcuts; all bindings can be changed on the Shortcuts page. Other actions are initially unassigned.

## Build

Use Visual Studio Build Tools MSBuild. `dotnet build` may fail on machines where the .NET SDK cannot find Appx/Pri packaging tasks. The app targets .NET 8 and publishes self-contained, so the target PC does not need a separately installed .NET runtime.

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' src\SpeakerHop\SpeakerHop.csproj /restore /p:Configuration=Release /p:Platform=x64 /m
```

## Create unsigned, self-contained MSIX

```powershell
.\build-package.ps1 -Version 1.0.0.0 -Publisher 'CN=Your Partner Center Publisher'
```

The script runs a clean self-contained .NET publish into `artifacts\publish-<version>`, validates that the .NET runtime and requested app version are present, copies only that publish output into a clean package layout, and excludes PDB files. Windows App Runtime 1.7 is a package dependency. `src\SpeakerHop\Package.appxmanifest` is the manifest template; package identity, version, architecture, publisher, and executable entry point are safely replaced for the requested package.

The unsigned package is written to `artifacts\SpeakerHop_<version>_x64.msix`. For Store submission, pass the Partner Center reserved identity and publisher values, add final brand assets/screenshots, sign or let Partner Center sign as appropriate, and run Windows App Certification Kit.

## Local release installation

GitHub releases include a development ZIP and an unsigned MSIX for signing or Store submission. The unsigned MSIX is not a directly installable, trusted production installer.

For the development ZIP, enable Windows Developer Mode and install Windows App Runtime 1.7 (x64). Extract the ZIP and run:

```powershell
.\install-local.ps1
```

The installer copies the package to a stable folder under LocalAppData, registers it without removing settings, and launches it. If package registration is unavailable under your user profile, choose a writable local folder outside the checkout:

```powershell
.\install-local.ps1 -InstallDirectory D:\Apps\SpeakerHop\1.0.30.0
```

Do not move or delete the installed folder. The app shows its main window on the first launch; subsequent launches respect **Show main window on startup**. When that setting is off, open settings from the tray icon's context menu.

Updates are staged and validated before the running app is stopped. An existing install folder is preserved; the installer selects a fresh sibling folder when the requested destination already exists. If registration fails, it attempts to restore the previous registration and restart the previous app. Any folder still used by a package registration is retained.

To run the unit tests and the installer transaction tests (the latter uses mocks and does not alter the installed app):

```powershell
dotnet test tests\SpeakerHop.Tests\SpeakerHop.Tests.csproj
.\tests\install-local.transaction.tests.ps1
```

## Store review note

Windows does not expose a public WinRT API for setting the default render endpoint. This app uses the desktop CoreAudio endpoint APIs plus the commonly used PolicyConfig COM interface, and therefore ships as a full-trust packaged desktop app with `runFullTrust`. Keep this explicit in the Store submission notes.

## Copyright, terms and privacy

Copyright © 2026 mmiyaji. All rights reserved. No open-source redistribution or modification license is granted for SpeakerHop itself. Third-party components retain their own licenses.

The Information tab includes offline-readable [copyright](src/SpeakerHop/Legal/Copyright.txt), [terms of use](src/SpeakerHop/Legal/Terms.txt), [privacy policy](src/SpeakerHop/Legal/Privacy.txt), and [third-party notices](src/SpeakerHop/Legal/ThirdPartyNotices.txt), along with support, releases and the settings location. The app does not record audio or automatically upload logs. Opening the Donation tab fetches an external button image; external links open the corresponding service in your browser.
