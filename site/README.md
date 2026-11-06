# SpeakerHop website

Chat Agent Bridge の `site/` をベースにした、ビルド不要の静的サイトです。

## ローカル確認

リポジトリルートで `python -m http.server 8080 --directory site` を実行し、`http://localhost:8080/` を開きます。英語版は `?lang=en`。

## 配置

`site/` を静的ホスティングの公開ルートに設定してください。Chat Agent Bridge と同じ Direct Upload 方式です。PR を `main` にマージすると `.github/workflows/cloudflare-pages.yml` が検証後に `site/` を Cloudflare Pages の `speakerhop` プロジェクトへ配信します。Cloudflare GitHub アプリのリポジトリ権限追加は不要です。既存の GitHub Pages 用 `docs/privacy-policy/` は維持し、同じ内容を `site/privacy-policy/` に同梱しています。ポリシー変更時は両方を更新してください。

CSS、言語メニュー、テーマ切り替え、GLightbox は隣の Chat Agent Bridge のサイト構成を利用。スクリーンショットは SpeakerHop 1.1.0 の日英素材です。

## 配信の初期設定

- Cloudflare Pages に Direct Upload プロジェクト `speakerhop` を作成します（本番ブランチ `main`）。
- このリポジトリの Actions secrets に `CLOUDFLARE_ACCOUNT_ID` と `CLOUDFLARE_API_TOKEN` を登録します。トークンは対象アカウントの Pages 編集権限に限定し、値をコミットしないでください。他リポジトリのトークンは共用しません。
- `node scripts/check-site.mjs` で公開前の静的検証を実行できます。
- 手動再配信は GitHub Actions の `Cloudflare Pages` → `Run workflow`（`main`）から実行できます。
- 初期 URL は Cloudflare が割り当てる Pages URL です。カスタムドメインは別途設定します。割り当てが `speakerhop.pages.dev` と異なる場合は `robots.txt` と `sitemap.xml` を更新してください。
