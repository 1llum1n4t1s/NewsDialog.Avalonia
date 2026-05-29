namespace NewsDialog;

/// <summary>
/// 英語の既定文字列セット。<see cref="NewsOptions.Strings"/> 未指定時に使われる。
/// </summary>
public sealed class DefaultNewsStrings : INewsStrings
{
    /// <summary>共有インスタンス。</summary>
    public static DefaultNewsStrings Instance { get; } = new();

    private DefaultNewsStrings()
    {
    }

    /// <inheritdoc />
    public string Title => "News";

    /// <inheritdoc />
    public string AllCategory => "All";

    /// <inheritdoc />
    public string LoadingMessage => "Loading announcements…";

    /// <inheritdoc />
    public string EmptyMessage => "There are no announcements.";

    /// <inheritdoc />
    public string ErrorHeader => "Failed to load announcements";

    /// <inheritdoc />
    public string Retry => "Retry";

    /// <inheritdoc />
    public string Close => "Close";

    /// <inheritdoc />
    public string EmergencyHeader => "Important notice";

    /// <inheritdoc />
    public string Acknowledge => "I understand";

    /// <inheritdoc />
    public string DefaultActionLabel => "Open";

    /// <inheritdoc />
    public string NewBadge => "NEW";
}
