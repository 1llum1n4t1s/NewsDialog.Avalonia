using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuperLightLogger;

namespace NewsDialog;

/// <summary>
/// お知らせ画面の状態と取得を司る ViewModel。
/// Window / UserControl いずれの提供レイヤーからも DataContext として再利用される。
/// </summary>
public sealed partial class NewsViewModel : ObservableObject
{
    private static readonly ILog log = LogManager.GetLogger(typeof(NewsViewModel));

    private readonly INewsSource _source;
    private readonly List<NewsItem> _all = new();
    private bool _blockingAcknowledged;

    /// <summary>取得元 (<see cref="INewsSource"/>) を直接渡して初期化する。</summary>
    public NewsViewModel(INewsSource source, NewsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Options = options ?? new NewsOptions();
    }

    /// <summary>フィード URL から <see cref="HttpJsonNewsSource"/> を内部生成する便利コンストラクタ。</summary>
    /// <param name="feedUrl">例: <c>https://lhamiel.nephilim.jp/news.json</c></param>
    /// <param name="options">表示・振る舞いオプション。</param>
    public NewsViewModel(string feedUrl, NewsOptions? options = null)
        : this(new HttpJsonNewsSource(feedUrl), options)
    {
    }

    /// <summary>表示・振る舞いオプション (読み取り専用)。</summary>
    public NewsOptions Options { get; }

    /// <summary>解決済み文字列セット。XAML バインディング用。</summary>
    public INewsStrings Strings => Options.ResolvedStrings;

    /// <summary>アクセントブラシ。null の場合はテーマ既定。</summary>
    public IBrush? AccentBrush => Options.AccentBrush;

    /// <summary>表示するお知らせ (カテゴリ フィルタ適用後)。</summary>
    public ObservableCollection<NewsItem> Items { get; } = new();

    /// <summary>カテゴリ タブ (先頭は「すべて」)。</summary>
    public ObservableCollection<string> Categories { get; } = new();

    // ---------------- 状態 ----------------

    /// <summary>読み込み状態。</summary>
    [ObservableProperty]
    public partial NewsLoadState State { get; set; } = NewsLoadState.Loading;

    /// <summary>選択中カテゴリ (「すべて」含む)。</summary>
    [ObservableProperty]
    public partial string? SelectedCategory { get; set; }

    /// <summary>選択中のお知らせ。</summary>
    [ObservableProperty]
    public partial NewsItem? SelectedItem { get; set; }

    /// <summary>取得失敗時のエラーメッセージ。</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    // ---------------- 最終結果 ----------------

    /// <summary>ウィンドウが確定した outcome。</summary>
    public NewsOutcome FinalOutcome { get; private set; } = NewsOutcome.Closed;

    /// <summary>アクションが押されたお知らせ (押されていなければ null)。</summary>
    public NewsItem? ActionItem { get; private set; }

    /// <summary>最終エラー (Failed 時)。</summary>
    public Exception? FinalError { get; private set; }

    // ---------------- 派生 (XAML 用) ----------------

    /// <summary>State == Loading</summary>
    public bool IsLoading => State == NewsLoadState.Loading;

    /// <summary>State == Loaded</summary>
    public bool IsLoaded => State == NewsLoadState.Loaded;

    /// <summary>State == Empty</summary>
    public bool IsEmpty => State == NewsLoadState.Empty;

    /// <summary>State == Failed</summary>
    public bool IsFailed => State == NewsLoadState.Failed;

    /// <summary>確認必須の緊急お知らせ (最重要・未確認の 1 件)。無ければ null。</summary>
    public NewsItem? BlockingItem
        => _blockingAcknowledged
            ? null
            : _all.FirstOrDefault(static i => i is { Severity: NewsSeverity.Emergency, IsBlocking: true });

    /// <summary>緊急ブロッキングを表示中か。true の間は通常一覧 (WebView 含む) を隠す。</summary>
    public bool ShowBlocking => BlockingItem is not null;

    /// <summary>選択中本文を WebView に流す URI。ContentUrl 優先、無ければ InlineHtml / Summary を data URI 化。</summary>
    public Uri? CurrentContentUri => BuildContentUri(SelectedItem);

    /// <summary>選択中お知らせにアクション URL があるか (ボタン表示制御)。</summary>
    public bool SelectedHasAction => SelectedItem?.ActionUrl is not null;

    /// <summary>選択中お知らせのアクションボタン ラベル (未指定時は既定文言)。</summary>
    public string SelectedActionLabel => SelectedItem?.ActionLabel ?? Strings.DefaultActionLabel;

    /// <summary>緊急ブロッキングにアクション URL があるか。</summary>
    public bool BlockingHasAction => BlockingItem?.ActionUrl is not null;

    /// <summary>緊急ブロッキングのアクションボタン ラベル。</summary>
    public string BlockingActionLabel => BlockingItem?.ActionLabel ?? Strings.DefaultActionLabel;

    // ---------------- プロパティ変更フック ----------------

    partial void OnStateChanged(NewsLoadState value)
    {
        log.InfoFormat("News state changed to {0}", value);
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFailed));
    }

    partial void OnSelectedItemChanged(NewsItem? value)
    {
        OnPropertyChanged(nameof(CurrentContentUri));
        OnPropertyChanged(nameof(SelectedHasAction));
        OnPropertyChanged(nameof(SelectedActionLabel));
        if (value is not null)
            Options.RaiseNewsOpened(value);
    }

    partial void OnSelectedCategoryChanged(string? value) => ApplyCategoryFilter();

    // ---------------- 取得 ----------------

    /// <summary>お知らせを取得し、結果に応じて Loaded / Empty / Failed へ遷移する。</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        State = NewsLoadState.Loading;
        try
        {
            var fetched = await _source.FetchAsync(Options.Context, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            _all.Clear();
            _all.AddRange(fetched
                .OrderByDescending(static i => i.Severity)
                .ThenByDescending(static i => i.PublishedAt));

            BuildCategories();

            SelectedCategory = Strings.AllCategory;
            ApplyCategoryFilter();
            SelectedItem = Items.FirstOrDefault();

            RaiseBlockingChanged();

            State = _all.Count == 0 ? NewsLoadState.Empty : NewsLoadState.Loaded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            FinalError = ex;
            State = NewsLoadState.Failed;
            log.Error("News load failed", ex);
            Options.RaiseErrorOccurred(ex);
        }
    }

    private void BuildCategories()
    {
        Categories.Clear();
        Categories.Add(Strings.AllCategory);
        foreach (var category in _all
                     .Select(static i => i.Category)
                     .Where(static c => !string.IsNullOrWhiteSpace(c))
                     .Select(static c => c!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Categories.Add(category);
        }
    }

    private void ApplyCategoryFilter()
    {
        var showAll = SelectedCategory is null
                      || string.Equals(SelectedCategory, Strings.AllCategory, StringComparison.Ordinal);

        Items.Clear();
        foreach (var item in _all)
        {
            if (showAll || string.Equals(item.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
                Items.Add(item);
        }

        if (SelectedItem is null || !Items.Contains(SelectedItem))
            SelectedItem = Items.FirstOrDefault();
    }

    // ---------------- コマンド ----------------

    /// <summary>お知らせを選択する。</summary>
    [RelayCommand]
    private void Select(NewsItem? item)
    {
        if (item is not null)
            SelectedItem = item;
    }

    /// <summary>アクションボタン押下。URL を開き (オプション)、ホストに通知する。</summary>
    [RelayCommand]
    private void InvokeAction(NewsItem? item)
    {
        var target = item ?? SelectedItem;
        if (target?.ActionUrl is null)
            return;

        ActionItem = target;
        FinalOutcome = NewsOutcome.ActionInvoked;

        if (Options.OpenActionUrlWithShell)
            TryOpenUrl(target.ActionUrl);

        Options.RaiseActionInvoked(target);
    }

    /// <summary>緊急ブロッキングを確認して通常表示へ切り替える。</summary>
    [RelayCommand]
    private void AcknowledgeBlocking()
    {
        _blockingAcknowledged = true;
        if (FinalOutcome == NewsOutcome.Closed)
            FinalOutcome = NewsOutcome.Acknowledged;

        RaiseBlockingChanged();
    }

    /// <summary>取得をやり直す。</summary>
    [RelayCommand]
    private Task Retry() => LoadAsync();

    // ---------------- ヘルパー ----------------

    private void RaiseBlockingChanged()
    {
        OnPropertyChanged(nameof(BlockingItem));
        OnPropertyChanged(nameof(ShowBlocking));
        OnPropertyChanged(nameof(BlockingHasAction));
        OnPropertyChanged(nameof(BlockingActionLabel));
    }

    private static Uri? BuildContentUri(NewsItem? item)
    {
        if (item is null)
            return null;

        if (item.ContentUrl is not null)
            return item.ContentUrl;

        var html = item.InlineHtml
                   ?? (item.Summary is { Length: > 0 } summary
                       ? $"<!doctype html><meta charset=\"utf-8\"><p>{WebUtility.HtmlEncode(summary)}</p>"
                       : null);

        return html is null
            ? null
            : new Uri("data:text/html;charset=utf-8," + Uri.EscapeDataString(html));
    }

    private void TryOpenUrl(Uri url)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = url.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            log.Error("Failed to open action URL", ex);
            Options.RaiseErrorOccurred(ex);
        }
    }
}
