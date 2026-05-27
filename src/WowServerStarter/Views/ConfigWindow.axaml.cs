using Avalonia.Controls;
using Avalonia.Interactivity;
using WowServerStarter.Models;
using WowServerStarter.ViewModels;

namespace WowServerStarter.Views;

public sealed partial class ConfigWindow : Window
{
    public ConfigWindow()
    {
        InitializeComponent();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        Close((DataContext as ConfigWindowViewModel)?.ToConfig() ?? new AppConfig());
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
