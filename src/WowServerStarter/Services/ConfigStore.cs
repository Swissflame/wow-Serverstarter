using System.Text.Json;
using WowServerStarter.Models;

namespace WowServerStarter.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "wow-serverstarter",
        "config.json");

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            return new AppConfig();
        }

        await using var stream = File.OpenRead(ConfigPath);
        return await JsonSerializer.DeserializeAsync<AppConfig>(stream, Options, cancellationToken)
            ?? new AppConfig();
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, Options, cancellationToken);
    }
}
