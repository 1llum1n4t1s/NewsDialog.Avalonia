using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NewsDialog;

namespace NewsDemoApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private static NewsItem[] SampleItems() =>
    [
        new NewsItem
        {
            Id = "1", Title = "アップデート 1.0.173 を公開しました", Category = "アップデート",
            Severity = NewsSeverity.Important, PublishedAt = DateTimeOffset.Now.AddDays(-1),
            InlineHtml = "<!doctype html><meta charset='utf-8'><h2>1.0.173</h2><ul><li>不具合修正</li><li>パフォーマンス改善</li></ul>",
        },
        new NewsItem
        {
            Id = "2", Title = "メンテナンスのお知らせ", Category = "メンテナンス",
            PublishedAt = DateTimeOffset.Now.AddDays(-3),
            Summary = "5/30 02:00-04:00 にサーバーメンテナンスを行います。",
        },
        new NewsItem
        {
            Id = "3", Title = "NewsDialog.Avalonia デモへようこそ", Category = "お知らせ",
            PublishedAt = DateTimeOffset.Now.AddDays(-10),
            InlineHtml = "<!doctype html><meta charset='utf-8'><p>ソーシャルゲーム風お知らせ画面のデモです <b>🎉</b></p>",
        },
    ];

    private async void OnShowNormal(object? sender, RoutedEventArgs e)
    {
        var result = await NewsWindow.ShowAsync(this, new InMemoryNewsSource(SampleItems()),
            new NewsOptions { Strings = new JapaneseNewsStrings() });
        Result.Text = $"Outcome: {result.Outcome}";
    }

    private async void OnShowEmergency(object? sender, RoutedEventArgs e)
    {
        var items = new[]
        {
            new NewsItem
            {
                Id = "emergency", Title = "重要: 自動更新の不具合について", Category = "重要",
                Severity = NewsSeverity.Emergency, IsBlocking = true, PublishedAt = DateTimeOffset.Now,
                Summary = "一部バージョンで自動更新が失敗する不具合が判明しました。" +
                          "お手数ですが下のボタンから最新版を手動でダウンロードしてください。",
                ActionLabel = "最新版をダウンロード",
                ActionUrl = new Uri("https://lhamiel.nephilim.jp/"),
            },
        };

        var options = new NewsOptions { Strings = new JapaneseNewsStrings() };
        options.ActionInvoked += item => Result.Text = $"アクション実行: {item.Title}";

        var result = await NewsWindow.ShowAsync(this, new InMemoryNewsSource(items), options);
        Result.Text = $"Outcome: {result.Outcome}";
    }

    private async void OnShowJapanese(object? sender, RoutedEventArgs e)
    {
        var result = await NewsWindow.ShowAsync(this, new InMemoryNewsSource(SampleItems()),
            new NewsOptions { Strings = new JapaneseNewsStrings() });
        Result.Text = $"Outcome: {result.Outcome}";
    }

    private async void OnShowFeed(object? sender, RoutedEventArgs e)
    {
        var options = new NewsOptions
        {
            Strings = new JapaneseNewsStrings(),
            Context = new NewsContext { AppVersion = "1.0.172", Locale = "ja", Channel = "release" },
        };

        var result = await NewsWindow.ShowAsync(this, "https://lhamiel.nephilim.jp/news.json", options);
        Result.Text = result.Error is null
            ? $"Outcome: {result.Outcome}"
            : $"Outcome: {result.Outcome} / {result.Error.Message}";
    }
}

/// <summary>日本語ローカライズの例。</summary>
file sealed class JapaneseNewsStrings : INewsStrings
{
    public string Title => "お知らせ";
    public string AllCategory => "すべて";
    public string LoadingMessage => "お知らせを読み込み中…";
    public string EmptyMessage => "現在お知らせはありません。";
    public string ErrorHeader => "お知らせの取得に失敗しました";
    public string Retry => "再試行";
    public string Close => "閉じる";
    public string EmergencyHeader => "重要なお知らせ";
    public string Acknowledge => "確認しました";
    public string DefaultActionLabel => "開く";
    public string NewBadge => "NEW";
}
