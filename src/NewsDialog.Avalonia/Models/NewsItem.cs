using System;
using System.Text.Json.Serialization;

namespace NewsDialog;

/// <summary>
/// 1 件のお知らせ。フィード JSON の 1 要素にそのままマップされる。
/// </summary>
public sealed class NewsItem
{
    /// <summary>一意 ID (識別・ログ用。既読管理はしない)。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>一覧・詳細に表示するタイトル。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>カテゴリ (タブ見出し)。例: お知らせ / アップデート / メンテナンス。null 可。</summary>
    public string? Category { get; set; }

    /// <summary>公開日時。一覧の並び順と表示に使う。</summary>
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>重要度。<see cref="NewsSeverity.Emergency"/> かつ <see cref="IsBlocking"/> で確認必須モーダル表示。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NewsSeverity>))]
    public NewsSeverity Severity { get; set; } = NewsSeverity.Normal;

    /// <summary>本文 HTML ページの URL (NativeWebView で表示)。<see cref="InlineHtml"/> と両方ある場合はこちら優先。</summary>
    public Uri? ContentUrl { get; set; }

    /// <summary>インライン HTML 本文 (サーバページを使わない場合)。data URI で WebView に流す。</summary>
    public string? InlineHtml { get; set; }

    /// <summary>一覧プレビュー / 緊急カードに出す要約テキスト。</summary>
    public string? Summary { get; set; }

    /// <summary>一覧サムネイル画像 URL。null 可。</summary>
    public Uri? ThumbnailUrl { get; set; }

    /// <summary>確認必須モーダルで前面ブロッキング表示するか (通常は <see cref="NewsSeverity.Emergency"/> と併用)。</summary>
    public bool IsBlocking { get; set; }

    /// <summary>アクションボタンのラベル。例: 「手動更新ページを開く」。null かつ <see cref="ActionUrl"/> も null ならボタン非表示。</summary>
    public string? ActionLabel { get; set; }

    /// <summary>アクションボタン押下時に開く URL。</summary>
    public Uri? ActionUrl { get; set; }

    /// <summary>対象とする最小アプリバージョン (含む)。クライアント側フィルタ用。null = 下限なし。例: "1.0.0"。</summary>
    public string? MinAppVersion { get; set; }

    /// <summary>対象とする最大アプリバージョン (含む)。クライアント側フィルタ用。null = 上限なし。例: "1.0.172"。</summary>
    public string? MaxAppVersion { get; set; }

    /// <summary>対象ロケール (例: ["ja","en"])。null / 空 = 全ロケール。</summary>
    public string[]? Locales { get; set; }

    /// <summary>表示期限。これを過ぎたら一覧から除外。null = 無期限。</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>新着バッジ対象か (公開から <see cref="NewsDefaults.NewBadgeDays"/> 日以内)。表示専用。</summary>
    [JsonIgnore]
    public bool IsNew => (DateTimeOffset.UtcNow - PublishedAt).TotalDays <= NewsDefaults.NewBadgeDays;
}
