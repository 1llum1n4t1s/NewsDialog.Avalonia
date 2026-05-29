namespace NewsDialog;

/// <summary>
/// ウィンドウのリサイズ挙動。
/// </summary>
public enum WindowResizeMode
{
    /// <summary>固定サイズ。<c>SizeToContent=WidthAndHeight</c> + <c>CanResize=False</c>。</summary>
    Fixed,

    /// <summary>可変サイズ。<c>CanResize=True</c> + <c>MinSize</c>/<c>MaxSize</c> を尊重。お知らせ画面の既定。</summary>
    Resizable,
}
