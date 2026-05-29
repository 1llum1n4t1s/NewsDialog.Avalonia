namespace NewsDialog;

/// <summary>
/// お知らせの重要度。一覧での強調表示と、緊急ブロッキング判定に使う。
/// </summary>
public enum NewsSeverity
{
    /// <summary>通常のお知らせ。</summary>
    Normal,

    /// <summary>重要 (一覧で強調表示)。</summary>
    Important,

    /// <summary>緊急。<see cref="NewsItem.IsBlocking"/> と併用で確認必須モーダルを前面表示する。</summary>
    Emergency,
}
