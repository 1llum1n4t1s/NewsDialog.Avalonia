namespace NewsDialog;

/// <summary>
/// 配信バックエンドへ送る実行コンテキスト。<see cref="HttpJsonNewsSource"/> がクエリ文字列に載せるため、
/// Cloudflare Worker 側で「特定バージョンだけ緊急告知を返す」等の動的ターゲティングが可能になる。
/// 静的 JSON 配信時はクライアント側 (<see cref="NewsItem.MinAppVersion"/> 等) でフィルタされる。
/// </summary>
public sealed class NewsContext
{
    /// <summary>現在のアプリバージョン。例: "1.0.172"。</summary>
    public string? AppVersion { get; set; }

    /// <summary>UI ロケール。例: "ja"。</summary>
    public string? Locale { get; set; }

    /// <summary>配信チャンネル。例: "release"。</summary>
    public string? Channel { get; set; }
}
