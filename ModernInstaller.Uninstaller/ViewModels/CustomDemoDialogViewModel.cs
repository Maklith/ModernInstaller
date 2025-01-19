using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using ModernInstaller.Uninstaller.Views;
using Ursa.Controls;

namespace ModernInstaller.Uninstaller.ViewModels;

public partial class CustomDemoDialogViewModel : ObservableObject
{

    [ObservableProperty] private string _info;
    

    public CustomDemoDialogViewModel(string info)   
    {
        Info = info;
    }

    [RelayCommand]
    private void Exit()
    {
        Environment.Exit(2);
    }

  
  
}