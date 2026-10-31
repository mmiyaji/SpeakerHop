# SpeakerHop 1.0.26.0 validation — 2026-10-31

## Fixes and packaging

- Main window activation now respects `ShowMainWindowOnStartup` after the initial launch.
- The package build performs a clean, self-contained .NET publish and explicitly includes generated `SpeakerHop.pri` (renamed to `resources.pri`) and `App.xbf`. Without these files, the published app failed with a XAML resource lookup error for `TabViewScrollButtonBackground`.
- Visual Studio MSBuild discovery supports both Build Tools and Community installations.
- `install-local.ps1` copies the loose package into a stable installation directory before registration, preserving settings and package identity.
- On this PC, registration below LocalAppData failed with `0x80070003`; registration at `D:\Apps\SpeakerHop\1.0.26.0` succeeded. Repository-relative deployment paths are no longer used for the installed application.

## Validation

- Release build and self-contained publish: passed, zero build warnings/errors.
- Existing unit tests: 55 passed, zero failed/skipped.
- MSIX creation: passed. MSIX is unsigned, for signing or Store submission; the development ZIP supports local registration.
- Final published binaries were installed and launched through their registered Windows application identity.
- UI automation: all eight checks passed:
  1. Setting off starts without a visible window.
  2. Enabling the setting persists to disk.
  3. Setting on opens the main window on the next process launch.
  4. Windows startup remains enabled.
  5. Setting off remains hidden on the next process launch.
  6. Tray context menu opens settings; device and shortcut navigation work.
  7. Closing the main window keeps the tray process alive.
  8. Closing the main window hides it.
- Settings screenshot inspected: controls and descriptions visible, no clipping.
- New installed app and staging DLL hashes matched.

The PC was not rebooted. Startup visibility was tested with fresh process launches using the registered application identity. This does not constitute Microsoft Store certification or submission.

## Code review

Reviewed the pending application, settings persistence, hotkey registration, audio switching, startup registration, and packaging diffs before release. No release-blocking issue remained after fixing the missing published WinUI resources. The release also includes the previously pending fixes for stable device cycling order, atomic settings persistence, shortcut editing/registration rollback, and responsive settings UI.
