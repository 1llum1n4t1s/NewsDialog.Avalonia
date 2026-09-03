# NewsDialog.Avalonia 設計

この文書は現行実装の設計上の正本です。パッケージの導入・利用方法は [`README.md`](README.md)、開発時の規約と検証手順は [`AGENTS.md`](AGENTS.md) を参照してください。

## 目的と範囲

`NewsDialog.Avalonia` は、ホスト Avalonia アプリへ一覧・本文・緊急ブロッキング告知を提供する `net10.0` ライブラリです。静的 JSON と HTTP/JSON バックエンドを同じ契約で扱い、アプリのバージョンやロケールに応じた告知を届けます。

ライブラリは表示と取得を担い、既読状態・ユーザーアカウント・お知らせの編集管理は保持しません。`server/` の静的フィードと Cloudflare Worker は配信例であり、パッケージの実行時依存ではありません。

## 構成と責務

| 領域 | 主な要素 | 責務と境界 |
| --- | --- | --- |
| 公開 UI | `NewsWindow` / `NewsView` / `NewsViewModel` | `NewsWindow.ShowAsync` はウィンドウ表示と結果返却を簡便化する。`NewsView` は任意のホストへ埋め込め、`NewsViewModel` が状態・選択・コマンドを保持する。 |
| 取得 | `INewsSource`、`HttpJsonNewsSource`、`InMemoryNewsSource` | 取得元を UI から分離する。標準ソースは `NewsContext` を受け、HTTP/JSON または渡された項目をフィルタ済みの一覧として返す。独自 `INewsSource` は取得責務を持つ。 |
| モデルとターゲティング | `NewsItem`、`NewsContext`、`NewsFilter`、`NewsJsonContext` | フィード契約、期限・ロケール・バージョンのクライアント側フィルタ、AOT 対応 JSON 変換を定義する。 |
| 表示基盤 | `NativeWebView`、テーマ、コンバーター | 一覧は Avalonia、本文は OS ネイティブ WebView で描画する。緊急カードは純 Avalonia で描画する。 |
| 配信例と検証 | `server/news.json`、`server/worker/`、`samples/`、`tests/` | 静的フィード、任意の Worker ターゲティング例、対話デモ、クライアントと Worker の回帰テストを提供する。 |

## データフロー

1. ホストは `NewsWindow.ShowAsync`、または `NewsViewModel` と `NewsView` を使って `INewsSource` と `NewsOptions` を渡す。
2. `HttpJsonNewsSource` は `appVersion`、`locale`、`channel` をクエリに追加してフィードを取得する。ルートの `items` 配列を読み、壊れた個別項目はログに残してスキップする。
3. 標準ソースは `NewsFilter` で期限・ロケール・バージョンを適用する。`NewsViewModel` は重要度降順、公開日時降順に並べ、カテゴリと選択中項目を更新する。
4. `NewsView` は通常コンテンツまたは未確認の緊急カードだけを表示する。本文 URI は `contentUrl`、`inlineHtml`、`summary` の順で解決される。
5. ユーザー操作は `NewsOptions` のイベントへ通知され、`NewsWindow.ShowAsync` は閉じた時点の `NewsResult` を返す。

Worker 例も同じ期限・ロケール・バージョン規則で絞り込むため、サーバ側ターゲティングとクライアント側フィルタを併用しても結果は狭まるだけです。`channel` はバックエンド拡張用に送信されますが、同梱 Worker は現在それで分岐しません。

## 重要な不変条件

- JSON は `NewsJsonContext` を介して個別項目をデシリアライズし、ライブラリ プロジェクトの `IsAotCompatible` と警告エラー化を維持する。バージョン比較は `System.Version` と数値 2〜4 要素の正規化を使う。
- バージョン制約がある項目は、アプリ側または項目側のバージョンが欠落・不正なら表示しない。ロケールは大文字小文字を区別せず、要求 `ja-JP` は対象 `ja` に一致するが、その逆には広げない。
- WebView の本文とトップレベル遷移は HTTP/HTTPS のみを許可する。ライブラリが生成するインライン本文の `data:` URI と WebView 初期化時の `about:blank` は例外とする。OS シェルで開くアクション URL も HTTP/HTTPS に限定する。
- `Emergency` かつ `IsBlocking` の項目は未確認の間、通常コンテンツと排他的に表示する。複数件は 1 件ずつ確認し終えるまでユーザー起因のウィンドウクローズを許可しない。
- 既読状態は保存せず、`NEW` バッジは公開日時から 7 日以内かで都度判定する。取得失敗は `Failed` と `ErrorOccurred` で通知し、再試行は失敗状態を解消して再取得する。

## 設計判断とトレードオフ

- **NativeAOT を優先する。** ソースジェネレータとフレームワーク同梱の `System.Version` を採用し、反射依存や追加のバージョン比較ライブラリを避ける。その代わり JSON 変換とバージョン形式の扱いを明示的に実装する。
- **本文はネイティブ WebView、緊急表示は Avalonia。** HTML 表現と軽量なホスト統合を両立する一方、ネイティブ WebView の airspace 制約がある。通常層と緊急層を `IsVisible` で排他にして重なりを防ぐ。
- **配信元を抽象化し、標準実装で二重に守る。** `INewsSource` で HTTP、インメモリ、独自実装を差し替えられる。Worker が先に絞り込んでもクライアント側でもフィルタするため、静的配信にも対応できる一方、制約付き項目は有効なコンテキストを必要とする。
- **対象絞り込みは fail-closed にする。** 不明なバージョンや不正な制約で限定告知を広く表示しない。誤配信の危険を抑える代わりに、ホストは有効な `NewsContext.AppVersion` を渡す必要がある。
- **既読管理を持たない。** ストレージ、アカウント、同期を不要にして緊急表示を単純に保つ。その代わり表示済みを跨いで記録する機能はホスト側の責務になる。
