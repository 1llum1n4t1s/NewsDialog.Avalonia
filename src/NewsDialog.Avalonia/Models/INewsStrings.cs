namespace NewsDialog;

/// <summary>
/// お知らせ画面の表示文字列を差し替えるための拡張点。
/// ホストアプリの i18n に合わせて実装し <see cref="NewsOptions.Strings"/> に差し込む。
/// </summary>
public interface INewsStrings
{
    /// <summary>ウィンドウ タイトル。例: <c>お知らせ</c>。</summary>
    string Title { get; }

    /// <summary>「すべて」カテゴリ タブ。例: <c>All</c>。</summary>
    string AllCategory { get; }

    /// <summary>読み込み中メッセージ。例: <c>Loading announcements…</c>。</summary>
    string LoadingMessage { get; }

    /// <summary>お知らせが無い時のメッセージ。例: <c>There are no announcements.</c>。</summary>
    string EmptyMessage { get; }

    /// <summary>取得失敗時のヘッダー。例: <c>Failed to load announcements</c>。</summary>
    string ErrorHeader { get; }

    /// <summary>再試行ボタン。例: <c>Retry</c>。</summary>
    string Retry { get; }

    /// <summary>閉じるボタン。例: <c>Close</c>。</summary>
    string Close { get; }

    /// <summary>緊急ブロッキング モーダルのヘッダー。例: <c>Important notice</c>。</summary>
    string EmergencyHeader { get; }

    /// <summary>緊急モーダルの確認ボタン。例: <c>I understand</c>。</summary>
    string Acknowledge { get; }

    /// <summary>アクションボタンの既定ラベル (<see cref="NewsItem.ActionLabel"/> 未指定時)。例: <c>Open</c>。</summary>
    string DefaultActionLabel { get; }

    /// <summary>新着バッジ文言。例: <c>NEW</c>。</summary>
    string NewBadge { get; }
}
