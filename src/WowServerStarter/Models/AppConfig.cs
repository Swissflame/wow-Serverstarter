namespace WowServerStarter.Models;

public sealed class AppConfig
{
    public string Host { get; set; } = "192.168.1.118";
    public int SshPort { get; set; } = 22;
    public string Username { get; set; } = "klaus";
    public string Password { get; set; } = "andrea00";
    public bool SoundEnabled { get; set; } = true;
    public int CheckIntervalSeconds { get; set; } = 10;
}
