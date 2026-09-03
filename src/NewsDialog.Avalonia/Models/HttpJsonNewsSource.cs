using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperLightLogger;

namespace NewsDialog;

/// <summary>
/// HTTP で JSON フィードを取得する標準ソース。R2 静的 JSON でも Cloudflare Worker でも同じく使える。
/// <para><see cref="NewsContext"/> の appVersion / locale / channel をクエリ文字列に載せるので、
/// Worker 側で「v1.0.172 以下にだけ緊急告知を返す」のような動的ターゲティングが可能。</para>
/// <para>JSON 解析は <see cref="NewsJsonContext"/> (ソースジェネレータ) 経由で NativeAOT 安全。</para>
/// </summary>
public sealed class HttpJsonNewsSource : INewsSource
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly ILog log = LogManager.GetLogger(typeof(HttpJsonNewsSource));

    private readonly Uri _baseUrl;
    private readonly HttpClient _http;

    /// <summary>フィード URL (文字列) から生成する。</summary>
    /// <param name="baseUrl">例: <c>https://lhamiel.kagayoi.com/news.json</c></param>
    /// <param name="httpClient">共有したい <see cref="HttpClient"/>。null なら内部の共有インスタンス。</param>
    public HttpJsonNewsSource(string baseUrl, HttpClient? httpClient = null)
        : this(new Uri(baseUrl, UriKind.Absolute), httpClient)
    {
    }

    /// <summary>フィード URL (Uri) から生成する。</summary>
    public HttpJsonNewsSource(Uri baseUrl, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        _baseUrl = baseUrl;
        _http = httpClient ?? SharedClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsItem>> FetchAsync(NewsContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var url = BuildUrl(_baseUrl, context);
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var items = DeserializeItems(document.RootElement);
        return NewsFilter.Apply(items, context);
    }

    private static IReadOnlyList<NewsItem> DeserializeItems(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("News feed root must be an object.");

        JsonElement? itemsElement = null;
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase))
            {
                itemsElement = property.Value;
                break;
            }
        }

        if (itemsElement is null || itemsElement.Value.ValueKind == JsonValueKind.Null)
            return Array.Empty<NewsItem>();
        if (itemsElement.Value.ValueKind != JsonValueKind.Array)
            throw new JsonException("News feed items must be an array.");

        var items = new List<NewsItem>(itemsElement.Value.GetArrayLength());
        var index = 0;
        foreach (var itemElement in itemsElement.Value.EnumerateArray())
        {
            try
            {
                if (itemElement.ValueKind != JsonValueKind.Object)
                    throw new JsonException("News item must be an object.");

                var item = itemElement.Deserialize(NewsJsonContext.Default.NewsItem);
                if (item is not null)
                    items.Add(item);
            }
            catch (JsonException ex)
            {
                log.Error($"Skipped malformed news item at index {index}", ex);
            }

            index++;
        }

        return items;
    }

    private static Uri BuildUrl(Uri baseUrl, NewsContext context)
    {
        var query = new StringBuilder();
        AppendParam(query, "appVersion", context.AppVersion);
        AppendParam(query, "locale", context.Locale);
        AppendParam(query, "channel", context.Channel);

        if (query.Length == 0)
            return baseUrl;

        var separator = string.IsNullOrEmpty(baseUrl.Query) ? "?" : "&";
        return new Uri(baseUrl.AbsoluteUri + separator + query.ToString().TrimStart('&'));
    }

    private static void AppendParam(StringBuilder query, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        query.Append('&').Append(key).Append('=').Append(Uri.EscapeDataString(value));
    }
}
