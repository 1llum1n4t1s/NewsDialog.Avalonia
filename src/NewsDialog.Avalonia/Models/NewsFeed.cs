using System;
using System.Collections.Generic;

namespace NewsDialog;

/// <summary>
/// お知らせフィード JSON のルート。
/// <code>{ "items": [ ... ], "generatedAt": "2026-05-29T00:00:00Z" }</code>
/// </summary>
public sealed class NewsFeed
{
    /// <summary>お知らせ一覧。</summary>
    public List<NewsItem> Items { get; set; } = new();

    /// <summary>生成日時 (任意)。</summary>
    public DateTimeOffset? GeneratedAt { get; set; }
}
