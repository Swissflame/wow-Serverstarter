using WowServerStarter.Models;

namespace WowServerStarter.ViewModels;

public sealed class ServerEntryViewModel : ViewModelBase
{
    public ServerEntryViewModel(ServerEntry model)
    {
        Model = model;
    }

    public ServerEntry Model { get; }

    public string Name => Model.Name;
    public string Type => Model.Type == ServerType.AuthServer ? "authserver" : "worldserver";
    public int Port => Model.Port;
    public string Process => Model.ProcessId?.ToString() ?? "-";
    public ServerStatus Status => Model.Status;
    public string StatusText => Model.Status switch
    {
        ServerStatus.Running => "läuft",
        ServerStatus.Stopped => "gestoppt",
        _ => "unbekannt"
    };

    public string StatusBrush => Model.Status switch
    {
        ServerStatus.Running => "#48C774",
        ServerStatus.Stopped => "#E25555",
        _ => "#E3B341"
    };

    public void Refresh()
    {
        OnPropertyChanged(nameof(Process));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
    }
}
