using Avalonia;
using System;
using System.IO.Compression;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ModernInstaller;

sealed class Program
{
  
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var buildAvaloniaApp = AppBuilder.Configure<App>();
        buildAvaloniaApp.UsePlatformDetect();
        buildAvaloniaApp.With(new Win32PlatformOptions()
        {
            DpiAwareness = Win32DpiAwareness.Unaware
        });
        buildAvaloniaApp.With(new FontManagerOptions()
        {
            DefaultFamilyName = "avares://ModernInstaller/Assets/HarmonyOS_Sans_SC_Regular.ttf#HarmonyOS Sans",
            FontFallbacks = new[]
            {
                new FontFallback()
                {
                    FontFamily =
                        new FontFamily("avares://ModernInstaller/Assets/HarmonyOS_Sans_SC_Regular.ttf#HarmonyOS Sans")
                }
            },
        });
        buildAvaloniaApp.With(new RenderOptions()
        {
            TextRenderingMode = TextRenderingMode.Antialias,
            EdgeMode = EdgeMode.Antialias,
            BitmapInterpolationMode = BitmapInterpolationMode.HighQuality,
        });
        buildAvaloniaApp.LogToTrace();
        return buildAvaloniaApp;
    }
}