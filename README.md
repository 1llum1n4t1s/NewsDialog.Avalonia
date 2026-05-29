# NewsDialog.Avalonia

Avalonia 12 で動く **ソーシャルゲーム風お知らせ画面** ライブラリ。
一覧はネイティブ描画、本文は公式 `NativeWebView`（Windows は WebView2）で HTML を表示します。
緊急時にユーザーへ確実に届ける **ブロッキング告知** に対応し、配信元は Cloudflare R2 / Workers などの HTTP/JSON フィードを差し替えるだけで切り替えられます。

> 🐈‍⬛ 「自動更新が壊れたから手動で更新して！」のような緊急アナウンスを、アプリ内から確実に伝えるために生まれました。

![screenshot](docs/screenshot.png)
<!-- スクリーンショットは docs/screenshot.png に配置してください -->

## 特徴

- 📰 **一覧＋詳細**のソシャゲ風レイアウト（カテゴリ タブ・新着バッジ・重要度カラー）
- 🌐 本文は **HTML**（公式 `NativeWebView`／OS ネイティブ WebView 利用でバイナリ軽量）
- 🚨 **緊急ブロッキング告知**（確認必須モーダル＋アクションボタンに URL を載せられる）
- ☁️ 配信は **HTTP/JSON**（R2 静的 JSON でも Cloudflare Worker でも同じ）。`appVersion` 等を送るので **特定バージョン狙い撃ち**も可能
- 🧩 **3 段提供**（`NewsWindow` / `NewsView` / `NewsViewModel`）でホストに合わせて柔軟に組み込み
- 🌏 文字列差し替えで多言語対応（`INewsStrings`）
- ⚡ **NativeAOT 安全**（`IsAotCompatible`・JSON はソースジェネレータ・反射ゼロ）

## インストール

```sh
dotnet add package NewsDialog.Avalonia
```

Windows では本文表示に **Microsoft Edge WebView2 ランタイム**が必要です（Windows 11 は標準搭載）。

## クイックスタート

### フィード URL から 1 行で表示

```csharp
using NewsDialog;

var result = await NewsWindow.ShowAsync(
    owner: this,
    feedUrl: "https://your-domain.example/news.json",
    options: new NewsOptions
    {
        Context = new NewsContext { AppVersion = "1.0.172", Locale = "ja", Channel = "release" },
    });
```

### 取得元（`INewsSource`）を渡す

```csharp
// コードで供給（テスト・独自取得時）
var source = new InMemoryNewsSource(
    new NewsItem
    {
        Id = "1",
        Title = "アップデートのお知らせ",
        Category = "アップデート",
        PublishedAt = DateTimeOffset.Now,
        InlineHtml = "<h2>1.0.173</h2><p>不具合を修正しました。</p>",
    });

await NewsWindow.ShowAsync(this, source);
```

### `NewsView` を自前ウィンドウに埋め込む

```xml
<Window xmlns:news="using:NewsDialog">
  <news:NewsView x:Name="NewsBody"/>
</Window>
```

```csharp
NewsBody.DataContext = new NewsViewModel("https://your-domain.example/news.json");
await ((NewsViewModel)NewsBody.DataContext).LoadAsync();
```

## フィード JSON フォーマット

`severity` は文字列（`"Normal"` / `"Important"` / `"Emergency"`）。フィールド名は camelCase です。

```json
{
  "generatedAt": "2026-05-29T00:00:00Z",
  "items": [
    {
      "id": "emergency-2026-05-29",
      "title": "重要: 自動更新の不具合について",
      "category": "重要",
      "severity": "Emergency",
      "isBlocking": true,
      "publishedAt": "2026-05-29T03:00:00Z",
      "summary": "一部バージョンで自動更新が失敗します。下のボタンから最新版を手動でダウンロードしてください。",
      "actionLabel": "最新版をダウンロード",
      "actionUrl": "https://your-domain.example/download",
      "maxAppVersion": "1.0.173"
    },
    {
      "id": "update-1.0.173",
      "title": "アップデート 1.0.173 を公開しました",
      "category": "アップデート",
      "severity": "Important",
      "publishedAt": "2026-05-28T10:00:00Z",
      "contentUrl": "https://your-domain.example/news/1.0.173.html"
    }
  ]
}
```

本文は **`contentUrl`（HTML ページ）優先**、無ければ `inlineHtml`、それも無ければ `summary` を表示します。

### ターゲティング（クライアント側フィルタ）

`minAppVersion` / `maxAppVersion` / `locales` / `expiresAt` を付けると、`NewsContext` に合致しないお知らせは自動で除外されます。
（Cloudflare Worker でサーバ側ターゲティングする場合も同じフィールド設計で組めます → [`server/`](server/) 参照）

## 緊急ブロッキング告知

`severity: "Emergency"` かつ `isBlocking: true` のお知らせがあると、通常一覧の前に **確認必須モーダル**を表示します。
ユーザーが「確認」を押すまで通常画面には進めません。`actionUrl` を付けるとアクションボタンが出て、OS 既定ブラウザで開けます。

## ローカライズ

`INewsStrings` を実装して `NewsOptions.Strings` に渡します。

```csharp
public sealed class JapaneseNewsStrings : INewsStrings
{
    public string Title => "お知らせ";
    public string AllCategory => "すべて";
    public string EmergencyHeader => "重要なお知らせ";
    public string Acknowledge => "確認しました";
    // … 残りも実装
}
```

## 主なオプション（`NewsOptions`）

| プロパティ | 既定 | 説明 |
|------|------|------|
| `Strings` | English | 表示文字列セット |
| `Context` | 空 | `AppVersion` / `Locale` / `Channel`（クエリ送信＝サーバ狙い撃ちに使える） |
| `ChromeMode` | `Custom` | カスタム タイトルバー / OS 標準フレーム |
| `ResizeMode` | `Resizable` | リサイズ可否 |
| `AccentBrush` | テーマ既定 | アクセント色 |
| `OpenActionUrlWithShell` | `true` | アクション URL を OS ブラウザで開く |
| `ActionInvoked` / `NewsOpened` / `ErrorOccurred` | – | コールバック |

## Cloudflare で配信する

R2 に静的 `news.json` を置くだけでも動きますが、Worker を挟むと **特定バージョンだけ緊急告知を返す**等の動的配信ができます。サンプルは [`server/`](server/) を参照してください。

## ライセンス

MIT License © ゆろち

## 開発者向け

内部設計・コーディング規約・NativeAOT に関する注意は [`CLAUDE.md`](CLAUDE.md) を参照してください。
