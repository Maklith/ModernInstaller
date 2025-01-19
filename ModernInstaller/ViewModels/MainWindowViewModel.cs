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
using System.Threading.Tasks;
using System.Timers;
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

namespace ModernInstaller.ViewModels;

public partial class MainWindowViewModel : ObservableValidator
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
                CantInstallReason ="安装路径不为空，请重新选择" ;
                OnPropertyChanged(nameof(CantInstallReason));
                return false;
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
            InstallPath = Is64 ? $"{Environment.GetEnvironmentVariable("ProgramFiles", EnvironmentVariableTarget.Machine)}\\{appName}" : $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\{appName}";
        }
       
    }

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
         Task.Run(() =>
        {
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
                                var process1 = new Process();	   
                                process1.StartInfo.FileName = @"powershell.exe";
                                process1.StartInfo.UseShellExecute = false;
                                process1.StartInfo.CreateNoWindow = true;
                                process1.StartInfo.RedirectStandardInput = true;
                                process1.Start();
                                process1.StandardInput.WriteLine("$WshShell = New-Object -comObject WScript.Shell");
                                process1.StandardInput.WriteLine($"$Shortcut = $WshShell.CreateShortcut(\"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\\{AppName}.lnk\")");
                                process1.StandardInput.WriteLine($"$Shortcut.TargetPath = \"{InstallPath + "\\" + CanExecutePath}\"");
                                process1.StandardInput.WriteLine("$Shortcut.Save()");
                                //shellLink.IconLocation = new IconLocation(InstallPath + "\\" + CanExecutePath, 0);
                                process1.StandardInput.WriteLine("exit");
                                process1.WaitForExit();
                            
                                if (File.Exists($"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk"))
                                {
                                    File.Delete($"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk");
                                }

                                var process = new Process();	   
                                process.StartInfo.FileName = @"powershell.exe";
                                process.StartInfo.RedirectStandardInput = true;
                                process.StartInfo.UseShellExecute = false;
                                process.StartInfo.CreateNoWindow = true;
                                process.Start();
                                process.StandardInput.WriteLine("$WshShell = New-Object -comObject WScript.Shell");
                                process.StandardInput.WriteLine($"$Shortcut = $WshShell.CreateShortcut(\"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk\")");
                                process.StandardInput.WriteLine($"$Shortcut.TargetPath = \"{InstallPath + "\\" + CanExecutePath}\"");
                                process.StandardInput.WriteLine($"$Shortcut.Save()");
                                process.StandardInput.WriteLine("exit");
                                process.WaitForExit();
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