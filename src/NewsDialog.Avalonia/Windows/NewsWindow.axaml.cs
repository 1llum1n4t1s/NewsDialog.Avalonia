using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NewsDialog;

/// <summary>
/// そのまま <c>ShowDialog()</c> できる完成形のお知らせウィンドウ。
/// 静的便利メソッド <see cref="ShowAsync(Window, INewsSource, NewsOptions, CancellationToken)"/> で 1 行呼び出しが可能。
/// </summary>
public partial class NewsWindow : Window
{
    private readonly NewsViewModel _viewModel;

    /// <summary>ViewModel をそのまま受け取って初期化する。</summary>
    public NewsWindow(NewsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;

        ApplyOptions(viewModel.Options);
    }

    /// <summary>パラメタレス コンストラクタ (Avalonia デザイナ用)。実行時は呼ばない。</summary>
    public NewsWindow() : this(CreateDesignViewModel())
    {
    }

    private static NewsViewModel CreateDesignViewModel()
        => new(new InMemoryNewsSource());

    /// <summary>
    /// 1 行呼び出しの便利メソッド。取得を開始しつつウィンドウを表示し、閉じた時点の結果を返す。
    /// </summary>
    /// <param name="owner">親ウィンドウ。null の場合は単独表示。</param>
    /// <param name="source">お知らせ取得元。</param>
    /// <param name="options">表示・振る舞いオプション。</param>
    /// <param name="cancellationToken">取得処理用キャンセル トークン。</param>
    public static async Task<NewsResult> ShowAsync(
        Window? owner,
        INewsSource source,
        NewsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var resolvedOptions = options ?? new NewsOptions();
        var vm = new NewsViewModel(source, resolvedOptions);
        var window = new NewsWindow(vm);

        // 取得はバックグラウンドで開始 (LoadAsync 内部で例外を捕捉し State=Failed にする)
        _ = LoadInBackgroundAsync(vm, cancellationToken);

        if (owner is not null)
            await window.ShowDialog(owner).ConfigureAwait(true);
        else
            await ShowStandaloneAsync(window).ConfigureAwait(true);

        return new NewsResult(vm.FinalOutcome, vm.ActionItem, vm.FinalError);
    }

    /// <summary>フィード URL から呼び出す便利オーバーロード。</summary>
    public static Task<NewsResult> ShowAsync(
        Window? owner,
        string feedUrl,
        NewsOptions? options = null,
        CancellationToken cancellationToken = default)
        => ShowAsync(owner, new HttpJsonNewsSource(feedUrl), options, cancellationToken);

    private static async Task LoadInBackgroundAsync(NewsViewModel vm, CancellationToken cancellationToken)
    {
        try
        {
            await vm.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // キャンセルは無視 (ウィンドウは通常通り閉じられる)
        }
    }

    private static Task ShowStandaloneAsync(Window window)
    {
        var tcs = new TaskCompletionSource<object?>();
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            window.Closed -= handler;
            tcs.TrySetResult(null);
        };
        window.Closed += handler;
        window.Show();
        return tcs.Task;
    }

    private void ApplyOptions(NewsOptions options)
    {
        // chrome
        switch (options.ChromeMode)
        {
            case WindowChromeMode.System:
                ExtendClientAreaToDecorationsHint = false;
                WindowDecorations = WindowDecorations.Full;
                CustomTitleBar.IsVisible = false;
                TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
                AcrylicBackdrop.IsVisible = false;
                SolidBackdrop.IsVisible = true;
                break;

            case WindowChromeMode.Custom:
                ExtendClientAreaToDecorationsHint = true;
                ExtendClientAreaTitleBarHeightHint = -1;
                WindowDecorations = WindowDecorations.BorderOnly;
                CustomTitleBar.IsVisible = true;
                break;
        }

        // サイズ
        switch (options.ResizeMode)
        {
            case WindowResizeMode.Fixed:
                SizeToContent = SizeToContent.WidthAndHeight;
                CanResize = false;
                if (options.InitialSize is { } fixedSize)
                {
                    SizeToContent = SizeToContent.Manual;
                    Width = fixedSize.Width;
                    Height = fixedSize.Height;
                }
                break;

            case WindowResizeMode.Resizable:
                SizeToContent = SizeToContent.Manual;
                CanResize = true;
                var init = options.InitialSize ?? NewsDefaults.InitialSize;
                Width = init.Width;
                Height = init.Height;
                MinWidth = options.MinSize.Width;
                MinHeight = options.MinSize.Height;
                if (options.MaxSize is { } max)
                {
                    MaxWidth = max.Width;
                    MaxHeight = max.Height;
                }
                break;
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }
}
