using System;
using System.Collections.Generic;
using System.Linq;

namespace NewsDialog;

/// <summary>
/// クライアント側ターゲティング フィルタ。静的 JSON 配信時のフォールバックで、
/// 期限切れ・ロケール不一致・バージョン範囲外のお知らせを除外する。
/// Worker 動的配信で既に絞り込まれている場合は二重適用しても無害。
/// <para>バージョン比較は <see cref="Version"/> (フレームワーク同梱・NativeAOT 安全) を使う。</para>
/// </summary>
internal static class NewsFilter
{
    public static IReadOnlyList<NewsItem> Apply(IReadOnlyList<NewsItem> items, NewsContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var appVer = TryParse(context.AppVersion);
        var result = new List<NewsItem>(items.Count);

        foreach (var it in items)
        {
            if (it.ExpiresAt is { } exp && exp < now)
                continue;

            if (context.Locale is { Length: > 0 } loc
                && it.Locales is { Length: > 0 } locales
                && !locales.Any(l => string.Equals(l, loc, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (appVer is not null)
            {
                if (TryParse(it.MinAppVersion) is { } min && appVer < min)
                    continue;
                if (TryParse(it.MaxAppVersion) is { } max && appVer > max)
                    continue;
            }

            result.Add(it);
        }

        return result;
    }

    private static Version? TryParse(string? value)
        => Version.TryParse(value, out var v) ? v : null;
}
