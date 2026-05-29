using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NewsDialog;

/// <summary>
/// コードで渡したお知らせをそのまま返すソース。テスト・デモ・ホスト側で独自取得した場合に使う。
/// </summary>
public sealed class InMemoryNewsSource : INewsSource
{
    private readonly IReadOnlyList<NewsItem> _items;

    /// <summary>お知らせ配列から生成する。</summary>
    public InMemoryNewsSource(params NewsItem[] items)
        => _items = items ?? Array.Empty<NewsItem>();

    /// <summary>お知らせ列挙から生成する。</summary>
    public InMemoryNewsSource(IEnumerable<NewsItem> items)
        => _items = new List<NewsItem>(items);

    /// <inheritdoc />
    public Task<IReadOnlyList<NewsItem>> FetchAsync(NewsContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(NewsFilter.Apply(_items, context));
    }
}
