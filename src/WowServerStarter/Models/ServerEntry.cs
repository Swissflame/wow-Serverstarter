namespace WowServerStarter.Models;

public enum ServerStatus
{
    Unknown,
    Running,
    Stopped
}

public enum ServerType
{
    AuthServer,
    WorldServer
}

public sealed class ServerEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ServerType Type { get; init; }
    public required string RootPath { get; init; }
    public required string BinaryName { get; init; }
    public int Port { get; init; }
    public int? ProcessId { get; set; }
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public string BinPath => $"{RootPath}/bin";
    public string LogFileName => Type == ServerType.AuthServer ? "authserver_launcher.log" : "worldserver_launcher.log";
}
