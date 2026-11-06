# SpeakerHop website

Chat Agent Bridge の `site/` をベースにした、ビルド不要の静的サイトです。

## ローカル確認

リポジトリルートで `python -m http.server 8080 --directory site` を実行し、`http://localhost:8080/` を開きます。英語版は `?lang=en`。

## 配置

`site/` を静的ホスティングの公開ルートに設定してください。Cloudflare の既存 Git 連携で、PR を `main` にマージすると配信されます。ビルド不要の静的サイトとして、出力ディレクトリは `site` を指定します。既存の GitHub Pages 用 `docs/privacy-policy/` は維持し、同じ内容を `site/privacy-policy/` に同梱しています。ポリシー変更時は両方を更新してください。

CSS、言語メニュー、テーマ切り替え、GLightbox は隣の Chat Agent Bridge のサイト構成を利用。スクリーンショットは SpeakerHop 1.1.0 の日英素材です。
