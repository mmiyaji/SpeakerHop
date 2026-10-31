# Microsoft Store 提出メモ — 1.1.0

## 更新メッセージ

日本語:

> UIの見直しと安定性の向上。
> デバイス選択・ショートカット・設定画面を整理し、キー名の表示、多言語対応、起動・更新時の動作を改善しました。

English:

> UI improvements and improved stability.
> Refined device selection, shortcuts and settings, with clearer key names, improved multilingual support, and more reliable startup and updates.

## パッケージ

- ファイル: `artifacts/SpeakerHop_1.1.0.0_x64.msix`
- Version: `1.1.0.0` / Architecture: `x64`
- Identity Name: `mmiyaji.SpeakerHop`
- Publisher: `CN=663C5D03-791A-4B2C-8509-629FA1CA125A`
- Windows App Runtime 1.7をパッケージ依存として使用。.NET 8ランタイムは同梱。
- GitHub配布用ZIPは開発者モード用。Storeへのアップロード対象はMSIX。
- Partner Centerの商品IdentityページでName、Publisher、PublisherDisplayNameの一致を確認済み。Store ID: `9NVNC6TQJN0Q`。

## 審査担当者向けの説明

SpeakerHop is a Windows tray utility that changes the default audio output, adjusts volume, and toggles output or microphone mute through user-configured global shortcuts. It does not record or transmit audio. Microphone mute targets the Windows default Multimedia capture endpoint.

The app declares `runFullTrust` because it uses desktop CoreAudio APIs and the PolicyConfig COM interface to change the default render endpoint. It does not install a driver or a service. After the first launch, the main window can be hidden according to the user's setting; open settings through the tray icon or launch the app again. The shortcut editor displays all available actions. Existing saved preferences are preserved during updates.

Settings and optional local crash logs remain on the PC and are not automatically uploaded. Opening the Donation tab fetches a button image from the Buy Me a Coffee CDN. External links open in the default browser.

## 公開ページと確認記録

- プライバシーポリシー: https://mmiyaji.github.io/SpeakerHop/privacy-policy/
- サポート: https://github.com/mmiyaji/SpeakerHop/issues
- [リリース説明](1.1.0.md)
- [検証記録](../validation-1.1.0.md)

## WACKの注記

WACK 10.0.26100.8249は全体PASS（24検査、必須失敗0件）。任意のApp Resources検査はPRIマップが商品Identityと一致するにもかかわらず0x80073B1Fを報告した。任意のBlocked Executables検査は同梱Microsoft .NETランタイムのcmd／reg／MSBuild等の文字列参照を報告した。アプリ独自にそれらの実行ファイルを同梱したものではない。

## 提出状況

Partner Centerのログイン済みChromeからSubmission 6（`1152921505702286064`）を準備中。商品Identityは照合済み。更新後のMSIXをアップロードし、価格・公開範囲・既存の年齢区分を維持する。提出と審査・一般公開は別の状態として記録する。
