using System;
using Avalonia;
using Avalonia.Media;

namespace NewsDialog;

/// <summary>
/// お知らせ画面 (<c>NewsWindow</c> / <c>NewsView</c>) のカスタマイズ オプション集。
/// 全プロパティに既定値があるため、ホスト側は差し替えたい項目だけ設定すれば良い。
/// </summary>
public sealed class NewsOptions
{
    // ---------------- ローカライゼーション ----------------

    /// <summary>表示文字列セット。null = <see cref="DefaultNewsStrings.Instance"/>。</summary>
    public INewsStrings? Strings { get; set; }

    /// <summary>解決済み文字列 (null セーフ アクセサ)。</summary>
    public INewsStrings ResolvedStrings => Strings ?? DefaultNewsStrings.Instance;

    // ---------------- ウィンドウ chrome / サイズ ----------------

    /// <summary>ウィンドウのフレーム描画モード。既定 = <see cref="WindowChromeMode.Custom"/>。</summary>
    public WindowChromeMode ChromeMode { get; set; } = WindowChromeMode.Custom;

    /// <summary>リサイズ挙動。既定 = <see cref="WindowResizeMode.Resizable"/> (お知らせ画面は広め)。</summary>
    public WindowResizeMode ResizeMode { get; set; } = WindowResizeMode.Resizable;

    /// <summary>明示的な初期サイズ。null = 既定 (<see cref="NewsDefaults.InitialSize"/>)。</summary>
    public Size? InitialSize { get; set; }

    /// <summary>リサイズ可能時の最小サイズ。</summary>
    public Size MinSize { get; set; } = NewsDefaults.MinSize;

    /// <summary>リサイズ可能時の最大サイズ。null = 無制限。</summary>
    public Size? MaxSize { get; set; }

    // ---------------- テーマ / 配色 ----------------

    /// <summary>アクセントカラー。バッジ / プライマリ ボタンに反映。null = テーマの既定。</summary>
    public IBrush? AccentBrush { get; set; }

    // ---------------- 取得 / 振る舞い ----------------

    /// <summary>配信バックエンドへ送る実行コンテキスト (appVersion / locale / channel)。</summary>
    public NewsContext Context { get; set; } = new();

    /// <summary>アクションボタン押下時、URL を OS 既定ブラウザで開くか。既定 = true。</summary>
    public bool OpenActionUrlWithShell { get; set; } = true;

    // ---------------- コールバック / イベント ----------------

    /// <summary>アクションボタンが押された時に発火。ホスト側で独自処理 (画面遷移等) をしたい場合に購読する。
    /// <para>このオプションは 1 ウィンドウ呼び出しにつき 1 インスタンスの利用を想定する。
    /// 同一インスタンスを使い回してハンドラを毎回登録すると二重発火やリークの原因になる。</para></summary>
    public event Action<NewsItem>? ActionInvoked;

    /// <summary>お知らせが選択 / 表示された時に発火。閲覧計測などに使う。</summary>
    public event Action<NewsItem>? NewsOpened;

    /// <summary>取得 / 表示でエラー発生時に発火。ホストのロガーへ流す想定。</summary>
    public event Action<Exception>? ErrorOccurred;

    internal void RaiseActionInvoked(NewsItem item) => ActionInvoked?.Invoke(item);

    internal void RaiseNewsOpened(NewsItem item) => NewsOpened?.Invoke(item);

    internal void RaiseErrorOccurred(Exception ex) => ErrorOccurred?.Invoke(ex);
}
