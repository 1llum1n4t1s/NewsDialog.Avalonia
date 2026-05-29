using Avalonia;

namespace NewsDialog;

/// <summary>
/// ライブラリ内のマジックナンバーを集約する内部定数。利用者は <see cref="NewsOptions"/> 経由で上書きする。
/// </summary>
internal static class NewsDefaults
{
    /// <summary>お知らせウィンドウの既定初期サイズ。</summary>
    public static readonly Size InitialSize = new(820, 560);

    /// <summary>お知らせウィンドウの既定最小サイズ。</summary>
    public static readonly Size MinSize = new(520, 380);

    /// <summary>「NEW」バッジを付ける公開からの経過日数のしきい値。</summary>
    public const int NewBadgeDays = 7;
}
