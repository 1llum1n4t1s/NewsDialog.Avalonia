using Avalonia.Controls;

namespace NewsDialog;

/// <summary>
/// お知らせ画面の中身を担う <see cref="UserControl"/>。
/// 単独利用時は任意のウィンドウに貼り付け、<c>DataContext</c> に <see cref="NewsViewModel"/> をセットする。
/// </summary>
public partial class NewsView : UserControl
{
    /// <summary>コンストラクタ。</summary>
    public NewsView()
    {
        InitializeComponent();
    }
}
