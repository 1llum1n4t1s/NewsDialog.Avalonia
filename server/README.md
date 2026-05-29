# 配信バックエンド サンプル

NewsDialog.Avalonia のフィードを配る 2 方式のサンプル。どちらもクライアント（ライブラリ）は無改修で切り替えられる。

## 方式 A: R2 静的 JSON（最小）

1. `news.json` を R2 バケットにアップロード。
2. R2 をカスタムドメインに公開（例: `https://lhamiel.nephilim.jp/news.json`）。
3. クライアント:
   ```csharp
   await NewsWindow.ShowAsync(this, "https://lhamiel.nephilim.jp/news.json", options);
   ```
4. ターゲティング（バージョン/ロケール/期限）は **クライアント側**で `minAppVersion` 等を見て自動フィルタ。

CDN キャッシュが効くので一番安い・速い。緊急時は `news.json` を差し替えるだけで全クライアントに届く。

## 方式 B: Cloudflare Worker（動的ターゲティング）

`worker/` に最小サンプル。クライアントが送る `?appVersion=&locale=&channel=` を見て、
**サーバ側で「壊れている 1.0.173 以下にだけ緊急告知を返す」**等の狙い撃ちができる（静的 JSON では不可）。

```sh
cd worker
npx wrangler deploy
```

- お知らせをコードに持つ最小例。運用では **Workers KV / D1 / R2** に移すと管理画面や即時更新がしやすい（`wrangler.toml` と `index.js` のコメント参照）。
- カスタムドメインの `/news.json` を Worker に向けるには `wrangler.toml` の `routes` を設定。

## フィード フォーマット

[`../README.md`](../README.md) の「フィード JSON フォーマット」を参照。`severity` は `"Normal"` / `"Important"` / `"Emergency"` の文字列、フィールド名は camelCase。
