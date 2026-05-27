using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WowServerStarter.Services;
using WowServerStarter.ViewModels;
using WowServerStarter.Views;

namespace WowServerStarter;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configStore = new ConfigStore();
            var sshService = new SshWowService();
            var soundService = new SoundService();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(configStore, sshService, soundService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
