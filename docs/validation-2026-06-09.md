# SpeakerHop validation notes - 2026-06-09

## Build and package

- Release build: passed
- MSIX package: `artifacts/SpeakerHop_1.0.0.13_x64.msix`

## Audio device enumeration performance

Measured by repeatedly enumerating active CoreAudio render devices through NAudio on the local machine.

- Iterations: 300
- Active render devices: 7
- Average: 3.62 ms
- P50: 3.25 ms
- P95: 5.29 ms
- Max: 38.55 ms

Result: enumeration is fast enough for the current refresh, tray click, and shortcut paths. The app re-enumerates devices on demand instead of polling in the background, so idle CPU impact should remain low.

## App startup and idle process snapshot

Measured with the Release build on the local machine.

- Startup to visible main window: 1,295 ms
- Working set after startup: 163.9 MB
- Private memory after startup: 102.3 MB
- CPU time after startup check: 1.52 s

Result: startup and memory use are acceptable for a WinUI 3 desktop tray utility, though the Windows App SDK runtime dominates the baseline memory footprint.

## Missing or stale device IDs

Tested by temporarily writing a non-existent device ID into the local settings file and starting the app.

Observed behavior:

- Current active devices are still listed from CoreAudio.
- Stale IDs are not shown as selectable current devices.
- The process remained alive after startup with stale settings.

Code hardening added:

- `AudioCommandService.GetDevices()`, `CycleDevice()`, `SetDevice()`, `ChangeVolume()`, and `ToggleMute()` now catch runtime audio exceptions and publish a status message instead of letting the app crash.
- This covers likely race conditions when a device disappears during enumeration or default-device switching.

## Dummy devices and physical plug/unplug

No fake audio driver was installed and no physical audio device was programmatically unplugged during this run. Windows audio render devices are driver-backed; a true dummy device requires installing a virtual audio driver or using actual hardware.

Expected behavior from implementation:

- Newly plugged active render devices appear after refreshing the device list or reopening the window.
- Removed devices are ignored by cycle selection because cycle candidates are built from the current active device list.
- A fixed-device shortcut pointing to a removed device reports that the specified device cannot be found.
- If a device is removed during an operation, the added exception handling prevents an app crash and shows a status message.

Recommended manual certification check:

1. Select two physical or virtual output devices in the Devices page.
2. Confirm tray-click cycling works.
3. Unplug one selected device.
4. Click the tray icon again and confirm the app stays alive.
5. Reopen the Devices page or click Refresh and confirm the removed device is gone.
6. Plug the device back in and confirm Refresh shows it again.
