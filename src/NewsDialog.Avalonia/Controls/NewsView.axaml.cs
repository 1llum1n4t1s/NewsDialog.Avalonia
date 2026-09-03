using System;
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

    private void OnWebViewNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (!IsAllowedContentNavigation(e.Request))
            e.Cancel = true;
    }

    private void OnWebViewNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        if (!IsAllowedContentNavigation(e.Request))
            e.Handled = true;
    }

    private static bool IsAllowedContentNavigation(Uri? uri)
        => uri is not null
           && (NewsViewModel.IsBrowserUrl(uri)
           || string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase)
           || string.Equals(uri.OriginalString, "about:blank", StringComparison.OrdinalIgnoreCase));
}
