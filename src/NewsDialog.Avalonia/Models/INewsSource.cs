using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NewsDialog;

/// <summary>
/// お知らせの取得元。HTTP/JSON (<see cref="HttpJsonNewsSource"/>) ・インメモリ
/// (<see cref="InMemoryNewsSource"/>) などを差し替えられる拡張点。
/// </summary>
public interface INewsSource
{
    /// <summary>お知らせを取得する。</summary>
    /// <param name="context">バージョン / ロケール / チャンネル等の実行コンテキスト。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    Task<IReadOnlyList<NewsItem>> FetchAsync(NewsContext context, CancellationToken cancellationToken = default);
}
