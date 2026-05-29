using System.Text.Json.Serialization;

namespace NewsDialog;

/// <summary>
/// System.Text.Json ソースジェネレータ コンテキスト。
/// 反射を使わずシリアライズ/デシリアライズするため NativeAOT / trim でも安全。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NewsFeed))]
[JsonSerializable(typeof(NewsItem))]
internal partial class NewsJsonContext : JsonSerializerContext
{
}
