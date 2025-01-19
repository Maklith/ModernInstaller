using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ModernInstaller.ViewModels;

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