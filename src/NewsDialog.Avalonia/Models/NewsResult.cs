using System;

namespace NewsDialog;

/// <summary>
/// お知らせウィンドウが閉じた時点の結果。<c>NewsWindow.ShowAsync</c> の戻り値。
/// </summary>
public sealed record NewsResult(NewsOutcome Outcome, NewsItem? ActionItem = null, Exception? Error = null);

/// <summary>
/// お知らせウィンドウ終了時の分岐。
/// </summary>
public enum NewsOutcome
{
    /// <summary>ユーザーが普通に閉じた。</summary>
    Closed,

    /// <summary>アクションボタンが押された (<c>ActionItem</c> 参照)。</summary>
    ActionInvoked,

    /// <summary>緊急ブロッキングを確認した。</summary>
    Acknowledged,

    /// <summary>お知らせの取得に失敗した (<c>Error</c> 参照)。</summary>
    Failed,
}
