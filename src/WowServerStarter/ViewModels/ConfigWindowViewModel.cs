using WowServerStarter.Models;

namespace WowServerStarter.ViewModels;

public sealed class ConfigWindowViewModel : ViewModelBase
{
    private string _host;
    private int _sshPort;
    private string _username;
    private string _password;
    private bool _soundEnabled;
    private int _checkIntervalSeconds;

    public ConfigWindowViewModel(AppConfig config)
    {
        _host = config.Host;
        _sshPort = config.SshPort;
        _username = config.Username;
        _password = config.Password;
        _soundEnabled = config.SoundEnabled;
        _checkIntervalSeconds = config.CheckIntervalSeconds;
    }

    public string Host { get => _host; set => SetProperty(ref _host, value); }
    public int SshPort { get => _sshPort; set => SetProperty(ref _sshPort, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public bool SoundEnabled { get => _soundEnabled; set => SetProperty(ref _soundEnabled, value); }
    public int CheckIntervalSeconds { get => _checkIntervalSeconds; set => SetProperty(ref _checkIntervalSeconds, value); }

    public AppConfig ToConfig()
    {
        return new AppConfig
        {
            Host = Host.Trim(),
            SshPort = Math.Clamp(SshPort, 1, 65535),
            Username = Username.Trim(),
            Password = Password,
            SoundEnabled = SoundEnabled,
            CheckIntervalSeconds = Math.Clamp(CheckIntervalSeconds, 3, 300)
        };
    }
}
