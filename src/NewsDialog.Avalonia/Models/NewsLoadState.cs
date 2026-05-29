namespace NewsDialog;

/// <summary>
/// お知らせ画面の読み込み状態。
/// </summary>
public enum NewsLoadState
{
    /// <summary>取得中。</summary>
    Loading,

    /// <summary>取得完了 (1 件以上)。</summary>
    Loaded,

    /// <summary>取得完了だがお知らせ 0 件。</summary>
    Empty,

    /// <summary>取得失敗。</summary>
    Failed,
}
