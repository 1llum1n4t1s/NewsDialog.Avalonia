using System;
using System.Collections.Generic;
using System.Globalization;
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
                && !locales.Any(l => MatchesLocale(loc, l)))
                continue;

            var hasMinVersion = !string.IsNullOrEmpty(it.MinAppVersion);
            var hasMaxVersion = !string.IsNullOrEmpty(it.MaxAppVersion);
            if (hasMinVersion || hasMaxVersion)
            {
                if (appVer is null)
                    continue;

                var min = hasMinVersion ? TryParse(it.MinAppVersion) : null;
                var max = hasMaxVersion ? TryParse(it.MaxAppVersion) : null;
                if ((hasMinVersion && min is null) || (hasMaxVersion && max is null))
                    continue;
                if (min is not null && appVer < min)
                    continue;
                if (max is not null && appVer > max)
                    continue;
            }

            result.Add(it);
        }

        return result;
    }

    private static bool MatchesLocale(string requestedLocale, string? itemLocale)
    {
        if (string.IsNullOrEmpty(itemLocale))
            return false;

        if (string.Equals(requestedLocale, itemLocale, StringComparison.OrdinalIgnoreCase))
            return true;

        return requestedLocale.Length > itemLocale.Length
               && requestedLocale.StartsWith(itemLocale, StringComparison.OrdinalIgnoreCase)
               && requestedLocale[itemLocale.Length] == '-';
    }

    private static Version? TryParse(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var parts = value.Split('.');
        if (parts.Length is < 2 or > 4)
            return null;

        Span<int> components = stackalloc int[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out components[index]))
                return null;
        }

        return new Version(components[0], components[1], components[2], components[3]);
    }
}
