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

## Unreleased follow-up from code review

The subsequent review identified additional failure paths. The working tree now protects unreadable settings against later writes, handles UI/startup persistence failures, limits key capture to the active window, retains startup shortcut registration warnings, redirects manual relaunches to the running instance, and stages installer updates with rollback.

Validation of these follow-up changes:

- Unit tests: 62 passed, including settings read-lock/recovery, failed initial-launch persistence, invalid-JSON backup failure, and interprocess activation requests.
- Release build with the WinUI workflow/analyzers: passed.
- Real UI checks: hidden startup followed by manual relaunch opened settings with one process; a locked settings file caused an error message and UI rollback without exiting or changing the existing file; Escape dismissed key capture; moving focus to a separate test window cancelled capture and permitted typed input in that window; startup shortcut conflicts remained visible after navigation.
- A locked file at startup kept the app alive, preserved the existing file, and displayed a persistent warning on the Devices and Settings pages.
- Installer tests use an isolated PowerShell process and mocked deployment APIs to verify staging, rollback, same-version development updates, and preservation of files still referenced by an active registration.
- The original installed application registration and user settings were restored after UI verification. These follow-up source changes have not been released.

## Local update to 1.0.27.0

At the user's request, the follow-up fixes were subsequently built as 1.0.27.0 and installed at `D:\Apps\SpeakerHop\1.0.27.0`. User preferences were compared before/after and preserved; installed executable, assembly, PRI and XBF hashes matched the package staging files. The prior 1.0.26.0 installation files and a settings backup were retained.

All eight startup/tray/UI checks passed against the installed 1.0.27.0 application. Manual relaunch opened settings in the existing process; the About page showed 1.0.27.0. The final process was left running with its main window hidden. Unit tests passed 62/62. GitHub publication remains on hold; the requested release title is **動作安定性の向上**.

## Local update to 1.0.28.0: microphone and output mute

Added `MicrophoneMuteToggle` as action value 5, preserving existing output `MuteToggle` value 4. The microphone action toggles the Windows default Capture/Multimedia endpoint; the output action continues to toggle the default Render/Multimedia endpoint. Action names and notifications distinguish the two in all supported languages. No new key assignments are added automatically.

Unit tests passed 65/65; Release build and MSIX creation succeeded. Installed 1.0.28.0 at `D:\Apps\SpeakerHop\1.0.28.0` and verified matching installed/package file hashes and preserved user preferences. Temporary test bindings exercised both actions through real global hotkeys: each endpoint toggled independently and toggled back to its original state. Both actions appeared in the shortcut UI. Test-only bindings were removed and original microphone/output mute states were restored. The updated application remains running in the tray; publication remains on hold.

## UI revision prepared as 1.0.29.0

- Captured and inspected the existing Shortcuts, Devices, Settings, Donation and About pages before editing (`artifacts/ui-density-before`).
- The shortcut editor now always lists all six operations. Each row has an enabled checkbox, key assignment, relevant device/volume control, and compact alternate-key/clear actions. Cycling buttons for changing action, target and volume step were removed.
- `ShortcutDraftService` preserves existing bindings and disabled rows, adds unassigned display rows, and omits unassigned rows from persisted settings. Regression tests cover round trips, multiple bindings, clearing, and input isolation.
- Devices/settings/about use denser aligned rows; command labels and explanatory copy are shorter. About displays the product version without the commit suffix, and duplicate website actions were removed.
- Native preview verification used a separate package identity, mutex/activation channel and settings directory so the user's running application and its unsaved draft remained untouched. Preview fixtures kept native shortcut bindings disabled to avoid competing with the installed app.
- Release builds and MSIX creation passed. Unit tests: 69 passed. Native UI checks confirmed six operation rows at 1040, 700 and 540 pixel widths, correct per-action controls, stable Save access, repeated direct edit/save behavior, unassigned-key capture validation, alternate binding add/clear, and light/dark rendering. Contrast themes were not tested.
- Narrow-width row overlap discovered during visual inspection was repaired, and the compact navigation rail remains visible to avoid covering page headings. Accepted screenshots and UIA reports are under `artifacts/local-update-1.0.29.0/screenshots`.
- Installation remains pending the user's choice about the currently unsaved shortcut changes. The installed 1.0.28.0 process was left running; no GitHub release was published.
