
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nuke.Common;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Serilog;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.PublishNativeInstaller);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    public static Guid uuid = Guid.NewGuid();

    Target BuildNativeUninstaller => _ => _
        .Executes(() =>
        {
            File.WriteAllText("Assets\\ApplicationUUID",uuid.ToString());
            DotNetTasks.DotNetPublish(c => new DotNetPublishSettings()
                .SetProject("ModernInstaller.Uninstaller")
                .SetOutput(RootDirectory / "Publish" )
                .SetFramework("net9.0-windows")
                .SetRuntime("win-x86")
                .SetConfiguration("Release")
                .SetSelfContained(true)
                .SetPublishSingleFile(true)
                
            );
        });
    Target PrepareBuildNativeInstaller => _ => _
        .DependsOn(BuildNativeUninstaller)
        .Executes(() =>
        {
            File.Copy(RootDirectory / "Publish" / "ModernInstaller.Uninstaller.exe",RootDirectory / "Assets" / "ModernInstaller.Uninstaller.exe",true);
        });
    Target BuildNativeInstaller => _ => _
        .DependsOn(PrepareBuildNativeInstaller)
        .Executes(() =>
        {
            DotNetTasks.DotNetPublish(c => new DotNetPublishSettings()
                //.SetProject("AvaloniaApplication1")
                .SetProject("ModernInstaller")
                .SetOutput(RootDirectory / "Publish" )
                .SetFramework("net9.0-windows")
                .SetRuntime("win-x86")
                .SetConfiguration("Release")
                .SetSelfContained(true)
                .SetPublishSingleFile(true)
                
            );
        });
    Target PublishNativeInstaller => _ => _
        .DependsOn(BuildNativeInstaller)
        .Executes(() =>
        {
            
        });
}
