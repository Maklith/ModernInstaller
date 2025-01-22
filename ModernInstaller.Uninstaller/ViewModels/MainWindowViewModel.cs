using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ModernInstaller.Models;
using ModernInstaller.Uninstaller.ViewModels;
using ModernInstaller.Uninstaller.Views;
using Ursa.Controls;

namespace ModernInstaller.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private bool nowBeforeInstall = true;
    [ObservableProperty] private bool nowInstall = false;
    [ObservableProperty] private int nowProgress =100;
    [ObservableProperty] private bool nowAfterInstall = false;

    private bool Is64;
    [ObservableProperty] private string appName="Modern Installer";
    
    private string MainFileFullPath =string.Empty;
    private string Path =string.Empty;
    public MainWindowViewModel()
    {
        Task.Run((() =>
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (var infoJsonS =
                   assembly.GetManifestResourceStream("ModernInstaller.Uninstaller.Assets.Installer.info.json"))
            {
                var bytes2 = new byte[infoJsonS.Length];
                infoJsonS.Read(bytes2, 0, bytes2.Length);
                var s2 = Encoding.UTF8.GetString(bytes2);
                var deserialize = JsonSerializer.Deserialize<Info>(s2, SourceGenerationContext.Default.Info);
                Is64 = deserialize.Is64;
            }

            using (var manifestResourceStream =
                   assembly.GetManifestResourceStream("ModernInstaller.Uninstaller.Assets.ApplicationUUID"))
            {
                var bytes = new byte[manifestResourceStream.Length];
                manifestResourceStream.ReadExactly(bytes, 0, bytes.Length);
                var s = Encoding.UTF8.GetString(bytes);
                var lpSubKey = $$"""SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{{{s}}}_ModernInstaller""";
                Console.WriteLine(lpSubKey);
                using (var openSubKey = RegistryKey
                           .OpenBaseKey(RegistryHive.LocalMachine,
                               Is64 ? RegistryView.Registry64 : RegistryView.Registry32).OpenSubKey(
                               lpSubKey))
                {
                    if (openSubKey is null)
                    {
                        ShowInfo("安装程序未找到");
                        return;
                    }

                    Console.WriteLine(openSubKey.ToString());

                    AppName = openSubKey.GetValue("DisplayName").ToString();
                    MainFileFullPath = openSubKey.GetValue("Path").ToString() + "\\" +
                                       openSubKey.GetValue("MainFile").ToString();
                    Path = openSubKey.GetValue("Path").ToString();

                }


                // ZipFile.ExtractToDirectory(manifestResourceStream,InstallPath,true);
            }
        }));


    }
    [RelayCommand]
    private async Task Install()
    {
        NowBeforeInstall = false;
        NowInstall = true;
        var minProgress = 100;
        await Task.Delay(500);
        var timer = new Timer(10);
        timer.AutoReset = true;
        timer.Elapsed += (sender, args) =>
        {
            
            if (NowProgress>=minProgress)
            {
                NowProgress--;
            }
        };
        timer.Start();
        Task.Run( (async() =>
        {
            minProgress = 70;
            //检查进程是否退出
            if (!TryKillProcess(MainFileFullPath))
            {
                ShowInfo("中止目标进程时出现错误,卸载被中止");
                return;
            }
            
           

            minProgress = 50;
            try
            {
                Directory.Delete(Path, true);
            }
            catch (Exception exception)
            {
                ShowInfo("文件删除时出现错误,卸载被中止");
                return;
            }
            
           
            minProgress = 0;
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (var manifestResourceStream =
                       assembly.GetManifestResourceStream("ModernInstaller.Uninstaller.Assets.ApplicationUUID"))
                {
                    var bytes = new byte[manifestResourceStream.Length];
                    manifestResourceStream.ReadExactly(bytes, 0, bytes.Length);
                    var s = Encoding.UTF8.GetString(bytes);
                    using (var openSubKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,Is64? RegistryView.Registry64: RegistryView.Registry32).OpenSubKey(
                               "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\",
                               RegistryKeyPermissionCheck.ReadWriteSubTree))
                    {

                        openSubKey.DeleteSubKey($$"""{{{s}}}_ModernInstaller""");
                    }
                    // ZipFile.ExtractToDirectory(manifestResourceStream,InstallPath,true);
                }
            }
            catch (Exception e)
            {
                ShowInfo("移除安装注册时出现问题,卸载被中止");
                return;
            }

            try
            {
                File.Delete($"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\\{AppName}.lnk");
                File.Delete($"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{AppName}.lnk");
            }
            catch (Exception e)
            {
                ShowInfo("移除快捷方式时出现错误,卸载近乎完成,请手动删除快捷方式");
                return;
            }
            NowInstall = false;
            NowAfterInstall = true;
        }));

    }
 private bool TryKillProcess(string processFilePath)
    {
         // PowerShell 脚本内容，直接嵌入
        string script = @"
param(
    [string]$processFilePath
)

function TryTerminateProcess {
    param(
        [string]$filePath
    )

    $maxAttempts = 10
    $attempt = 0

    while ($attempt -lt $maxAttempts) {
        # 获取所有匹配的进程
        $processes = Get-Process | Where-Object { $_.MainModule.FileName -eq $filePath }

        if ($processes) {
            foreach ($process in $processes) {
                Write-Host '尝试终止进程: ' $process.Name ' [' $process.Id ']'
                try {
                    $process.Kill()
                    Write-Host '成功终止进程: ' $process.Name ' [' $process.Id ']'
                } catch {
                    Write-Host '无法终止进程: ' $process.Name ' [' $process.Id ']'
                }
            }
        } else {
            Write-Host '没有找到匹配的进程。'
            return '正常退出'
        }

        # 增加尝试次数
        $attempt++
        Write-Host '尝试次数: $attempt'

        # 每秒钟尝试一次
        Start-Sleep -Seconds 1
    }

    if ($attempt -eq $maxAttempts) {
        Write-Host '达到最大尝试次数 ($maxAttempts)，停止尝试终止进程。'
        return '超出尝试次数'
    }
}

# 调用函数来开始反复尝试终止进程
TryTerminateProcess -filePath $processFilePath
";

        // 创建一个 Process 用来运行 PowerShell 脚本
        var process = new Process();
        process.StartInfo.FileName = "powershell.exe";
        process.StartInfo.Arguments = $"-Command \"{script}\" -processFilePath \"{processFilePath}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        // 启动进程
        process.Start();

        // 捕获输出和错误
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        // 输出执行结果
        Console.WriteLine("Output: ");
        Console.WriteLine(output);

        // 判断脚本是超出尝试次数还是正常退出
        if (output.Contains("超出尝试次数"))
        {
            return false;
        }
        else if (output.Contains("正常退出"))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(error))
        {
            return false;
        }
        return false;
    }
    [RelayCommand]
    private void Exit()
    {
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