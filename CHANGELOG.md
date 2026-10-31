# Changelog

All notable changes to SpeakerHop are recorded here. Version numbers distinguish the release name from the MSIX package version; release publication is tracked separately.

## 1.1.0 — Release preparation

- Refined settings layouts with compact, aligned controls and a visible highlight for the active output device.
- Improved shortcut editing and safer registration behavior. New installs use less common default shortcut combinations; existing saved bindings are preserved. Output mute and microphone mute remain separate actions, and no binding is assigned automatically to the microphone action.
- Named Space, editing, numpad, IME, browser and media keys; punctuation follows the active Windows keyboard layout. Modified Tab/Enter/Escape combinations can be captured while bare keys retain dialog navigation.
- Added confirmation before changing the application language and display language names consistently.
- Localized copyright, terms, privacy, and third-party notices for Japanese, English, Simplified Chinese, Traditional Chinese, Korean, German, French, and Spanish.
- Improved settings persistence, startup handling, single-instance activation across package updates, and installer staging and rollback.
- MSIX package version: `1.1.0.0`. Release candidate assets are prepared as a development ZIP and an unsigned x64 MSIX.

Release publication and final installation smoke testing are pending; this entry is a preparation record, not confirmation of a published release.
