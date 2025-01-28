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
using System.Threading;
using System.Threading.Tasks;
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
using Timer = System.Timers.Timer;

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
            if (!await TerminateProcess(MainFileFullPath))
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