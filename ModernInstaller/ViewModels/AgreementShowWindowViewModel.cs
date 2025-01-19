using System;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using ModernInstaller.Models;

namespace ModernInstaller.ViewModels;

public partial class AgreementShowWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string text;

    public AgreementShowWindowViewModel()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (var infoJsonS =
               assembly.GetManifestResourceStream("ModernInstaller.Assets.Agreement.txt"))
        {
            var bytes2 = new byte[infoJsonS.Length];
            infoJsonS.Read(bytes2, 0, bytes2.Length);
            var s2 = Encoding.UTF8.GetString(bytes2);
            Text = s2;
        }
    }
}