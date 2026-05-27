using System.Collections.ObjectModel;
using Avalonia.Threading;
using WowServerStarter.Models;
using WowServerStarter.Services;
using WowServerStarter.Views;

namespace WowServerStarter.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigStore _configStore;
    private readonly SshWowService _sshService;
    private readonly SoundService _soundService;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(10));
    private readonly CancellationTokenSource _disposeCts = new();
    private AppConfig _config = new();
    private string _message = "Bereit";
    private bool _isBusy;

    public MainWindowViewModel(ConfigStore configStore, SshWowService sshService, SoundService soundService)
    {
        _configStore = configStore;
        _sshService = sshService;
        _soundService = soundService;

        RefreshCommand = new RelayCommand(_ => DiscoverAsync());
        OpenConfigCommand = new RelayCommand(OpenConfigAsync);
        StartCommand = new RelayCommand(p => ExecuteOnServerAsync(p, s => _sshService.StartAsync(_config, s.Model, _disposeCts.Token)));
        StopCommand = new RelayCommand(p => ExecuteOnServerAsync(p, s => _sshService.StopAsync(_config, s.Model, _disposeCts.Token)));
        RebootCommand = new RelayCommand(p => ExecuteOnServerAsync(p, s => _sshService.RebootAsync(_config, s.Model, _disposeCts.Token)));

        _ = InitializeAsync();
        _ = PollLoopAsync();
    }

    public ObservableCollection<ServerEntryViewModel> Servers { get; } = [];
    public RelayCommand RefreshCommand { get; }
    public RelayCommand OpenConfigCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RebootCommand { get; }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                StartCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
                RebootCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task InitializeAsync()
    {
        _config = await _configStore.LoadAsync(_disposeCts.Token);
        ResetTimer();
        await DiscoverAsync();
    }

    private async Task DiscoverAsync()
    {
        await RunUiSafeAsync(async () =>
        {
            Message = "Suche Server...";
            var entries = await _sshService.DiscoverAsync(_config, _disposeCts.Token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Servers.Clear();
                foreach (var entry in entries)
                {
                    Servers.Add(new ServerEntryViewModel(entry));
                }
            });
            Message = entries.Count == 0 ? "Keine Server gefunden" : "Status aktuell";
        });
    }

    private async Task PollLoopAsync()
    {
        while (await _timer.WaitForNextTickAsync(_disposeCts.Token))
        {
            if (Servers.Count == 0 || IsBusy)
            {
                continue;
            }

            await RefreshStatusesAsync(false);
        }
    }

    private async Task RefreshStatusesAsync(bool showBusy)
    {
        await RunUiSafeAsync(async () =>
        {
            if (showBusy)
            {
                Message = "Aktualisiere...";
            }

            var previous = Servers.ToDictionary(s => s.Model.Id, s => s.Model.Status);
            await _sshService.RefreshAsync(_config, Servers.Select(s => s.Model).ToList(), _disposeCts.Token);

            foreach (var server in Servers)
            {
                if (previous.TryGetValue(server.Model.Id, out var oldStatus) && oldStatus != server.Model.Status)
                {
                    _soundService.Ping(_config.SoundEnabled);
                }

                server.Refresh();
            }

            Message = "Status aktuell";
        }, showBusy);
    }

    private async Task ExecuteOnServerAsync(object? parameter, Func<ServerEntryViewModel, Task> action)
    {
        if (parameter is not ServerEntryViewModel server)
        {
            return;
        }

        await RunUiSafeAsync(async () =>
        {
            var oldStatus = server.Model.Status;
            server.Model.Status = ServerStatus.Unknown;
            server.Model.ProcessId = null;
            server.Refresh();

            Message = $"{server.Name}: Aktion läuft...";
            try
            {
                await action(server);
                await Task.Delay(TimeSpan.FromSeconds(2), _disposeCts.Token);
                await _sshService.RefreshAsync(_config, [server.Model], _disposeCts.Token);
            }
            finally
            {
                server.Refresh();
            }

            if (oldStatus != server.Model.Status)
            {
                _soundService.Ping(_config.SoundEnabled);
            }

            Message = $"{server.Name}: fertig";
        });
    }

    private async Task OpenConfigAsync(object? _)
    {
        var dialog = new ConfigWindow
        {
            DataContext = new ConfigWindowViewModel(_config)
        };

        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            var result = await dialog.ShowDialog<AppConfig?>(desktop.MainWindow);
            if (result is not null)
            {
                _config = result;
                await _configStore.SaveAsync(_config, _disposeCts.Token);
                ResetTimer();
                await DiscoverAsync();
            }
        }
    }

    private async Task RunUiSafeAsync(Func<Task> action, bool showBusy = true)
    {
        try
        {
            if (showBusy)
            {
                IsBusy = true;
            }

            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Fehler: {ShortError(ex)}";
        }
        finally
        {
            if (showBusy)
            {
                IsBusy = false;
            }
        }
    }

    private void ResetTimer()
    {
        _timer.Period = TimeSpan.FromSeconds(Math.Clamp(_config.CheckIntervalSeconds, 3, 300));
    }

    private static string ShortError(Exception exception)
    {
        var message = exception.Message;
        if (message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Login abgelehnt", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Passwort", StringComparison.OrdinalIgnoreCase))
        {
            return "Passwort falsch";
        }

        if (message.Contains("Verbindung", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No route", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return "Verbindung fehlgeschlagen";
        }

        return string.IsNullOrWhiteSpace(message) ? "SSH-Befehl fehlgeschlagen" : message;
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _timer.Dispose();
        _disposeCts.Dispose();
    }
}
