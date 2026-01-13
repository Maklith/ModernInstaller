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
        if (args != null && System.Linq.Enumerable.Contains(args, "--silent"))
        {
            try
            {
                var viewModel = new ModernInstaller.ViewModels.MainWindowViewModel();
                viewModel.IsSilent = true;
                viewModel.Agreed = true;

                if (viewModel.CanInstall)
                {
                    System.Console.WriteLine("Starting silent installation...");
                    viewModel.Install().GetAwaiter().GetResult();
                    System.Console.WriteLine("Installation complete. Launching application...");
                    viewModel.LaunchApplication();
                    return;
                }
                else
                {
                    System.Console.WriteLine($"Cannot install: {viewModel.CantInstallReason}");
                    System.Environment.Exit(1);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Silent installation failed: {ex.Message}");
                System.Console.WriteLine(ex.StackTrace);
                System.Environment.Exit(1);
                return;
            }
        }

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