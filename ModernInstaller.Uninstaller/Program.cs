using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ModernInstaller;

sealed class Program
{
  
    public static string ApplicationUUID ;
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ShellExecute(
        [Optional] IntPtr hwnd,
        [Optional] string? lpOperation,
        string lpFile,
        [Optional] string? lpParameters,
        [Optional] string? lpDirectory,
        int nShowCmd);
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        //检查当前程序是否在TEMP目录
       
        if (!Debugger.IsAttached&& AppContext.BaseDirectory != Path.GetTempPath())
        {
            //复制此程序到TEMP目录,并退出
            Directory.CreateDirectory(Path.GetTempPath());
            var sourceFileName = AppContext.BaseDirectory + "ModernInstaller.Uninstaller.exe";
            File.Copy(sourceFileName, Path.GetTempPath() + "ModernInstaller.Uninstaller.exe", true);
                
            ShellExecute(IntPtr.Zero, "runas", Path.GetTempPath() + "ModernInstaller.Uninstaller.exe", "", "",
                1);
            Environment.Exit(0);
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
            DefaultFamilyName = "avares://ModernInstaller.Uninstaller/Assets/HarmonyOS_Sans_SC_Regular.ttf#HarmonyOS Sans",
            FontFallbacks = new[]
            {
                new FontFallback()
                {
                    FontFamily =
                        new FontFamily("avares://ModernInstaller.Uninstaller/Assets/HarmonyOS_Sans_SC_Regular.ttf#HarmonyOS Sans")
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