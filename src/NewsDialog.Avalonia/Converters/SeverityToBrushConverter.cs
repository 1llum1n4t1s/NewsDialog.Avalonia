using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NewsDialog;

/// <summary>
/// <see cref="NewsSeverity"/> を一覧の左アクセント色に変換する。
/// 固定色を返すだけなので反射不要・NativeAOT 安全。
/// </summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    /// <summary>共有インスタンス。</summary>
    public static SeverityToBrushConverter Instance { get; } = new();

    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF));
    private static readonly IBrush ImportantBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23));
    private static readonly IBrush EmergencyBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4D));

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            NewsSeverity.Emergency => EmergencyBrush,
            NewsSeverity.Important => ImportantBrush,
            _ => NormalBrush,
        };

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
