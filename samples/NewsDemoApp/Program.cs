using Avalonia;
using Microsoft.Extensions.Logging;

namespace NewsDemoApp;

internal static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        // NewsDialog.Avalonia 内部の SuperLightLogger ログを拾うための設定。
        // 呼ばないと NullLoggerFactory になりライブラリのログが出力されない。
        SuperLightLogger.LogManager.Configure(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
