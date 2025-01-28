using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ModernInstaller.Models;
using ModernInstaller.Views;
using Timer = System.Timers.Timer;

namespace ModernInstaller.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private bool nowUnstall;
    [ObservableProperty] private bool nowBeforeInstall = true;
    [ObservableProperty] private bool nowInstall = false;
    [ObservableProperty] private int nowProgress = 0;
    [ObservableProperty] private bool nowAfterInstall = false;
    
    [ObservableProperty] private bool showDetail;
    [ObservableProperty] private string appName="Modern Installer";
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [ObservableProperty] private bool agreed = false;

    private bool Is64 = false;
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [ObservableProperty] private string installPath=$"C:\\Program Files\\";
    [NotifyPropertyChangedFor(nameof(IsUpdate))]
    [ObservableProperty] private Version installVersion;
    [ObservableProperty] private Version? hadInstalledVersion;
    private string? hadInstalledPath;
    public bool IsUpdate
    {
        get
        {
            if (HadInstalledVersion is null)
            {
                return false;
            }
            return InstallVersion>= HadInstalledVersion;
        }
    }

    public bool CanInstall
    {
        get
        {
            if (Is64)
            {
                if (!Environment.Is64BitOperatingSystem)
                {
                    CantInstallReason ="X86架构无法安装X64程序" ;
                    OnPropertyChanged(nameof(CantInstallReason));
                    return false;
                }
            }
            if (string.IsNullOrWhiteSpace(InstallPath))
            {
                CantInstallReason ="安装路径为空，请选择安装目录" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
            }
            if (Directory.Exists(InstallPath)&& (Directory.EnumerateDirectories(InstallPath).Any()||  Directory.EnumerateFiles(InstallPath).Any()))
            {
                if (!IsUpdate)
                {
                    CantInstallReason = "安装路径不为空，请重新选择";
                    OnPropertyChanged(nameof(CantInstallReason));
                    return false;
                }
               
            }

            var pathRoot = Path.GetPathRoot(InstallPath);
            if (string.IsNullOrWhiteSpace(pathRoot))
            {
                CantInstallReason ="安装路径错误" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
            }

            if (!Agreed)
            {
                CantInstallReason ="请同意用户协议" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
            }

            return true;
        }
    }
    public string CantInstallReason { get; set; }

    public MainWindowViewModel()
    {
       
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (var infoJsonS =
               assembly.GetManifestResourceStream("ModernInstaller.Assets.Installer.info.json"))
        {
            var bytes2 = new byte[infoJsonS.Length];
            infoJsonS.Read(bytes2, 0, bytes2.Length);
            var s2 = Encoding.UTF8.GetString(bytes2);
            var deserialize = JsonSerializer.Deserialize<Info>(s2,SourceGenerationContext.Default.Info);
            var appName = deserialize.DisplayName;
            Is64 = deserialize.Is64;
            AppName = appName;
            InstallVersion = new Version(deserialize.DisplayVersion);
            TryGetHadInstalledVersion();
            if (hadInstalledPath is not null)
            {
                InstallPath = hadInstalledPath;
            }
            else
            {
                if (Environment.Is64BitOperatingSystem&&Is64)
                {
                    using (var openSubKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(
                               "SOFTWARE\\Microsoft\\Windows\\CurrentVersion",
                               RegistryKeyPermissionCheck.ReadWriteSubTree))
                    {
                        InstallPath =$"{openSubKey.GetValue( "ProgramFilesDir").ToString()}\\{appName}";
                    }
                }else
                    InstallPath =  $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\{appName}";
            }
           
        }
        
       
    }

    private void TryGetHadInstalledVersion()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (var manifestResourceStream =
               assembly.GetManifestResourceStream("ModernInstaller.Assets.ApplicationUUID"))
        {
            var bytes = new byte[manifestResourceStream.Length];
            manifestResourceStream.Read(bytes, 0, bytes.Length);
            var s = Encoding.UTF8.GetString(bytes);
            using (var openSubKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,Is64? RegistryView.Registry64: RegistryView.Registry32).OpenSubKey(
                       "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\",
                       RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                using (var registryKey = openSubKey.OpenSubKey($$"""{{{s}}}_ModernInstaller"""))
                {
                    if (registryKey is not null)
                    {
                        var version = registryKey.GetValue("DisplayVersion")?.ToString();
                        if (version is null)
                        {
                            return;
                        }
                        HadInstalledVersion = new Version(version);
                        hadInstalledPath= registryKey.GetValue("Path")?.ToString();
                        CanExecutePath = registryKey.GetValue("MainFile")?.ToString();
                    }
                }
            }
        }
    }
/// <summary>
    /// 尝试终止指定路径的进程
    /// </summary>
    /// <param name="processFilePath">进程文件路径</param>
    /// <returns>终止结果消息</returns>
    static async Task<bool> TerminateProcess(string processFilePath)
    {
        int maxAttempts = 10;
        int attempt = 0;

        while (attempt < maxAttempts)
        {
            // 获取当前进程列表
            string taskListCommand = $"wmic process where \"ExecutablePath='{processFilePath.Replace("\\","\\\\")}'\" get ProcessId,Name";
            var processList = ExecuteCommand(taskListCommand);

            if (string.IsNullOrWhiteSpace(processList))
            {
                return true;
            }

            // 处理每个进程
            string[] processes = processList.Split(new[] { "\r\n", "\n","\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var process in processes)
            {
                if (process.StartsWith("Name"))
                {
                    continue;
                }
                string[] processInfo = process.Split(' ',StringSplitOptions.RemoveEmptyEntries);
                string processName = processInfo[0];
                string processId = processInfo[1];

                Console.WriteLine($"尝试终止进程: {processName} [{processId}]");

                string killCommand = $"taskkill /f /pid {processId}";
                string killResult = ExecuteCommand(killCommand);

                if (killResult.Contains("成功"))
                {
                    Console.WriteLine($"成功终止进程: {processName} [{processId}]");
                }
                else
                {
                    Console.WriteLine($"无法终止进程: {processName} [{processId}]");
                }
            }

            attempt++;
            Console.WriteLine($"尝试次数: {attempt}");

            // 每秒钟等待一次
           await Task.Delay(1000);
        }

        return false;
    }

    /// <summary>
    /// 执行命令并获取输出
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <returns>命令的输出结果</returns>
    static string ExecuteCommand(string command)
    {
        try
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/C {command}";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("执行命令时发生错误: " + ex.Message);
            return string.Empty;
        }
    }
    [DllImport("ModernInstallerLib", CharSet = CharSet.Unicode,CallingConvention = CallingConvention.Cdecl)]
    public static extern int create_shortcut(
        [MarshalAs(UnmanagedType.LPWStr)] string targetPath,
        [MarshalAs(UnmanagedType.LPWStr)] string shortcutPath,
        [MarshalAs(UnmanagedType.LPWStr)] string description
    );
    [RelayCommand]
    private async Task ShowAgreement(Window control)
    {
        var agreementShowWindow = new AgreementShowWindow();
        agreementShowWindow.DataContext = new AgreementShowWindowViewModel();
        await agreementShowWindow.ShowDialog(control);
    }
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task Install()
    {
        NowBeforeInstall = false;
        NowInstall = true;
        var maxProgress = 0;
        await Task.Delay(500);
        var timer = new Timer(10);
        timer.AutoReset = true;
        timer.Elapsed += (sender, args) =>
        {
            
            if (NowProgress<maxProgress)
            {
                NowProgress++;
            }
        };
        timer.Start();
        Task.Run(async () =>
        {
            maxProgress = 20;
            
            var mainFilePath = $"{InstallPath}\\{CanExecutePath}";
            if (!await TerminateProcess(mainFilePath))
            {
                ShowInfo("中止目标进程时出现错误,卸载被中止");
                return;
            }
            maxProgress = 50;
            Assembly assembly = Assembly.GetExecutingAssembly();

            try
            {
                Directory.CreateDirectory(InstallPath);
                using (var manifestResourceStream = assembly.GetManifestResourceStream("ModernInstaller.Assets.App.zip"))
                {
                    ZipFile.ExtractToDirectory(manifestResourceStream, InstallPath, true);
                }
            }
            catch (Exception e)
            {
                ShowInfo("解压程序时出现错误,安装被中止");
                return;
            }

            maxProgress = 70;
            try
            {
                using (var manifestResourceStream =
                       assembly.GetManifestResourceStream(
                           "ModernInstaller.Assets.ModernInstaller.Uninstaller.exe"))
                {
                    using (var fileStream = new FileStream(Path.Combine(InstallPath, "ModernInstaller.Uninstaller.exe"),
                               FileMode.Create))
                    {
                        manifestResourceStream.CopyTo(fileStream);
                    }

                }
                using (var manifestResourceStream =
                       assembly.GetManifestResourceStream(
                           "ModernInstaller.Assets.Installer.info.json"))
                {
                    using (var fileStream = new FileStream(Path.Combine(InstallPath, "info.json"),
                               FileMode.Create))
                    {
                        manifestResourceStream.CopyTo(fileStream);
                    }

                }
            }
            catch (Exception e)
            {
                ShowInfo("创建卸载程序时出现错误,安装被中止");
                return;
            }

            maxProgress = 90;
            try
            {
                using (var manifestResourceStream =
                       assembly.GetManifestResourceStream("ModernInstaller.Assets.ApplicationUUID"))
                {
                    var bytes = new byte[manifestResourceStream.Length];
                    manifestResourceStream.Read(bytes, 0, bytes.Length);
                    var s = Encoding.UTF8.GetString(bytes);
                    using (var openSubKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,Is64? RegistryView.Registry64: RegistryView.Registry32).OpenSubKey(
                               "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\",
                               RegistryKeyPermissionCheck.ReadWriteSubTree))
                    {
                        using (var registryKey = openSubKey.CreateSubKey($$"""{{{s}}}_ModernInstaller"""))
                        {
                            using (var infoJsonS =
                                   assembly.GetManifestResourceStream("ModernInstaller.Assets.Installer.info.json"))
                            {
                                var bytes2 = new byte[infoJsonS.Length];
                                infoJsonS.Read(bytes2, 0, bytes2.Length);
                                var s2 = Encoding.UTF8.GetString(bytes2);
                                var deserialize =
                                    JsonSerializer.Deserialize<Info>(s2, SourceGenerationContext.Default.Info);
                                registryKey.SetValue("DisplayName", deserialize.DisplayName);
                                registryKey.SetValue("DisplayVersion", deserialize.DisplayVersion);
                                registryKey.SetValue("Publisher", deserialize.Publisher);
                                registryKey.SetValue("Path", InstallPath);
                                registryKey.SetValue("UninstallString", InstallPath + "\\ModernInstaller.Uninstaller.exe");
                                CanExecutePath = deserialize.CanExecutePath;
                                registryKey.SetValue("MainFile", CanExecutePath);
                                if (string.IsNullOrWhiteSpace(deserialize.DisplayIcon))
                                {
                                    registryKey.SetValue("DisplayIcon", InstallPath + "\\" + CanExecutePath + ",0");
                                }
                                else
                                {
                                    registryKey.SetValue("DisplayIcon", deserialize.DisplayIcon);
                                }

                                registryKey.SetValue("InstallDate", DateTime.Now.ToString("yyyy-MM-dd"));
                                if (File.Exists($"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\\{AppName}.lnk"))
                                {
                                    File.Delete($"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\\{AppName}.lnk");
                                }
                                maxProgress = 99;
                                create_shortcut(InstallPath + "\\" + CanExecutePath,
                                    $"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\\{AppName}.lnk",
                                    "");
                                if (File.Exists($"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk"))
                                {
                                    File.Delete($"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk");
                                }
                                create_shortcut(InstallPath + "\\" + CanExecutePath,
                                    $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk",
                                    "");
                                maxProgress = 100;
                                //shellLin2k.IconLocation = new IconLocation(InstallPath + "\\" + CanExecutePath, 0);
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                ShowInfo("写入注册表或创建快捷方式时出现错误,安装被中止");
                return;
            }
            NowAfterInstall = true;
            NowInstall = false;
        });


    }
    private string CanExecutePath = "";
    [RelayCommand]
    private void ShowDetailC()
    {
        ShowDetail = !ShowDetail;
    }

    [RelayCommand]
    private async Task PickFolder()
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime)
        {
            var tryGetFolderFromPathAsync = await classicDesktopStyleApplicationLifetime.MainWindow.StorageProvider.TryGetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            var folders= await classicDesktopStyleApplicationLifetime.MainWindow.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions()
                {
                    SuggestedStartLocation = tryGetFolderFromPathAsync
                });
            if (!folders.Any())
            {
                return; 
            }
            var path = "";
            try
            {
                path=  folders.First().Path.LocalPath;
            }
            catch (Exception e)
            {
                path=  folders.First().Path.OriginalString;
            }
            if (Directory.Exists(path)&& (Directory.EnumerateDirectories(path).Any()||  Directory.EnumerateFiles(path).Any()))
            {
                InstallPath = path+AppName;
            }
            else
            {
                InstallPath = path.TrimEnd('\\');
            }
            
        }
    }

    [RelayCommand]
    private void JustClose()
    {
        Environment.Exit(0);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ShellExecute(
        [Optional] IntPtr hwnd,
        [Optional] string? lpOperation,
        string lpFile,
        [Optional] string? lpParameters,
        [Optional] string? lpDirectory,
        int nShowCmd);
    [RelayCommand]
    private void CloseAndLaunch()
    {
        ShellExecute(IntPtr.Zero, "open", CanExecutePath, "", InstallPath,
            1);
        Environment.Exit(0);
    }
    private async Task ShowInfo(string info)
    {
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime applicationLifetime)
        {
            await Dispatcher.UIThread.InvokeAsync((async () =>
            {
                var customDemoDialog = new CustomDemoDialog();
                customDemoDialog.DataContext = new CustomDemoDialogViewModel(info);
                await customDemoDialog.ShowDialog(applicationLifetime.MainWindow);
            }));
            
        }
    }
}