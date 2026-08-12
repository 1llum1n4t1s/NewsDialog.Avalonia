# NewsDialog.Avalonia — 開発者・Claude Code 向けメモ

このファイルは、このリポジトリで作業する Claude Code 向けのガイダンスを提供する。Avalonia 12 のお知らせ画面ライブラリ。姉妹ライブラリ `VelopackUpdateDialog.Avalonia` と作法を揃えている。

## 立ち位置 / 経緯

- 大ヒット中の Lhamiel（NativeAOT ビルド）で自動更新が壊れたインシデントを受け、**緊急アナウンスをアプリ内で確実に届ける**ために新設。
- したがって **NativeAOT 安全性は最優先制約**。消費側（Lhamiel）が NativeAOT のため、NativeAOT で動くコードだけを入れる（AOT で動かないコードは入れない）。

## アーキテクチャ（3 段提供）

- `NewsViewModel`（状態機械＋取得）→ `NewsView`（UserControl／一覧＋本文＋緊急層）→ `NewsWindow`（Window／静的 `ShowAsync`）。
- 取得は `INewsSource` 抽象。標準実装は `HttpJsonNewsSource`（R2/Worker 両対応）と `InMemoryNewsSource`。
- バックエンド非依存。`NewsContext`（appVersion/locale/channel）をクエリに載せ、Worker 側で動的ターゲティング可能。静的 JSON 時は `NewsFilter` がクライアント側で代替フィルタ。

## NativeAOT で守ること

- JSON は **`NewsJsonContext`（System.Text.Json ソースジェネレータ）経由**で読む（`JsonSerializer` の反射オーバーロードは使わない）。
- バージョン比較は **`System.Version`**（フレームワーク同梱・AOT 安全）を使う。`NuGet.Versioning` は入れない（Lhamiel の AOT クラッシュの原因がこれだった）。
- `csproj` に `<IsAotCompatible>true</IsAotCompatible>` を入れて trim/AOT 解析を常時オンにする。`TreatWarningsAsErrors=true` なので **警告が出たらビルドが落ちる＝AOT 違反を早期検出**する設計。
- 新しい依存を足すときは、まず NativeAOT 対応を確認してから入れる。

## WebView の扱い（重要）

- 公式 `Avalonia.Controls.WebView` の `NativeWebView`（Windows=WebView2 / mac=WKWebView / Linux=WPE）。`Source`（Uri）に URL、本文インライン時は `data:` URI 化（`NewsViewModel.BuildContentUri`）。
- **ネイティブ WebView は airspace 制約**（Avalonia の上に常に重なる）。緊急ブロッキング層は **通常コンテンツと `IsVisible` で排他**にして、ブロッキング中は WebView を含む通常層を非表示にすることで重なり崩れを回避している。緊急カード自体は WebView を使わず純 Avalonia（テキスト）で描く。

## コーディング規約（姉妹ライブラリ準拠）

- `Options` は全項目デフォルト値＋`ResolvedStrings` null セーフ＋`event` と `internal Raise...` のペア。
- 文字列は `INewsStrings` ＋ `DefaultNewsStrings.Instance`。
- 結果は `record NewsResult` ＋ `enum NewsOutcome`。
- CommunityToolkit.Mvvm の `[ObservableProperty] public partial` と `[RelayCommand]` を使う。
- ログは SuperLightLogger（`LogManager.GetLogger`）。

## ビルド / 動作確認

```sh
dotnet build NewsDialog.Avalonia.slnx -c Debug          # 0 警告 0 エラーを維持する
dotnet run --project samples/NewsDemoApp           # デモ（緊急ブロッキングは Lhamiel インシデント模擬）
```

- AOT 実機検証をするなら、`samples/NewsDemoApp` を `PublishAot` で publish し、更新フロー同様に「緊急表示→アクション→本文 WebView」が落ちないかを確認する。

## 既読管理について

- 仕様上 **既読管理はしない（表示のみ）**。「NEW」バッジは公開日からの経過日数（`NewsDefaults.NewBadgeDays`）で出す recency バッジ。

## ドメイン移行（2026-07 開始・期限 2027/05/31）

屋号を **Kagayoi** に統一したため、配信ドメインを `nephilim.jp` から `kagayoi.com` へ移行中。方針の全体像はユーザーグローバルの `CLAUDE.md` §屋号とドメイン を参照する。

- **旧ドメイン `nephilim.jp` はレジストラで廃止申請済みで 2027/05/31 に失効する**（延長しない）。それまでに出荷済みバイナリを新ドメインへ移行しきる。
- 旧ホストの Worker route / custom domain は**期限まで消さない**。消すと出荷済みアプリの自動更新が止まる。
- `nephilim.jp` の Redirect Rules は `/` だけを 301 する。`releases.*.json` / `*.nupkg` / `*-Setup.exe` は転送せず R2 が配信を続ける。
- 配信先ホストは `lhamiel.kagayoi.com`（`wrangler.toml` の routes はコメントアウト状態）。旧 `lhamiel.nephilim.jp` を参照する記述が残っていないか、変更時に確認する。
