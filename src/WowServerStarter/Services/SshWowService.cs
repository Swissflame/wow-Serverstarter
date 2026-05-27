using System.Text;
using Renci.SshNet;
using WowServerStarter.Models;

namespace WowServerStarter.Services;

public sealed class SshWowService
{
    private static readonly string[] KnownRoots =
    [
        "/opt/azeroth-server",
        "/opt/azeroth-playerbots-server"
    ];

    public async Task<IReadOnlyList<ServerEntry>> DiscoverAsync(AppConfig config, CancellationToken cancellationToken)
    {
        return await WithClientAsync(config, async client =>
        {
            var result = new List<ServerEntry>();
            foreach (var root in KnownRoots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var authExists = await FileExistsAsync(client, $"{root}/bin/authserver", cancellationToken);
                var worldExists = await FileExistsAsync(client, $"{root}/bin/worldserver", cancellationToken);

                if (authExists)
                {
                    result.Add(new ServerEntry
                    {
                        Id = $"{root}:authserver",
                        Name = root.Contains("playerbots", StringComparison.OrdinalIgnoreCase) ? "Playerbots Auth" : "AzerothCore Auth",
                        Type = ServerType.AuthServer,
                        RootPath = root,
                        BinaryName = "authserver",
                        Port = 3724
                    });
                }

                if (worldExists)
                {
                    result.Add(new ServerEntry
                    {
                        Id = $"{root}:worldserver",
                        Name = root.Contains("playerbots", StringComparison.OrdinalIgnoreCase) ? "Playerbot Realm" : "Realm 1",
                        Type = ServerType.WorldServer,
                        RootPath = root,
                        BinaryName = "worldserver",
                        Port = root.Contains("playerbots", StringComparison.OrdinalIgnoreCase) ? 8086 : 8085
                    });
                }
            }

            foreach (var server in result)
            {
                await RefreshStatusAsync(client, server, cancellationToken);
            }

            return result;
        }, cancellationToken);
    }

    public async Task RefreshAsync(AppConfig config, IList<ServerEntry> servers, CancellationToken cancellationToken)
    {
        await WithClientAsync(config, async client =>
        {
            foreach (var server in servers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RefreshStatusAsync(client, server, cancellationToken);
            }

            return true;
        }, cancellationToken);
    }

    public async Task StartAsync(AppConfig config, ServerEntry server, CancellationToken cancellationToken)
    {
        await WithClientAsync(config, async client =>
        {
            var command = $"cd {Quote(server.BinPath)} && nohup ./{server.BinaryName} > ../logs/{server.LogFileName} 2>&1 &";
            await RunAsync(client, command, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await RefreshStatusAsync(client, server, cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task StopAsync(AppConfig config, ServerEntry server, CancellationToken cancellationToken)
    {
        await WithClientAsync(config, async client =>
        {
            var pattern = $"{server.RootPath}/bin/{server.BinaryName}";
            await RunAsync(client, $"pkill -TERM -f {Quote(pattern)} || true", cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            await RefreshStatusAsync(client, server, cancellationToken);

            if (server.Status == ServerStatus.Running)
            {
                await RunAsync(client, $"pkill -KILL -f {Quote(pattern)} || true", cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                await RefreshStatusAsync(client, server, cancellationToken);
            }

            return true;
        }, cancellationToken);
    }

    public async Task RebootAsync(AppConfig config, ServerEntry server, CancellationToken cancellationToken)
    {
        await StopAsync(config, server, cancellationToken);
        await StartAsync(config, server, cancellationToken);
    }

    private static async Task RefreshStatusAsync(SshClient client, ServerEntry server, CancellationToken cancellationToken)
    {
        var pattern = $"{server.RootPath}/bin/{server.BinaryName}";
        var pidOutput = await RunAsync(client, $"pgrep -f {Quote(pattern)} | head -n 1 || true", cancellationToken);
        server.ProcessId = int.TryParse(pidOutput.Trim(), out var pid) ? pid : null;

        if (server.ProcessId.HasValue)
        {
            server.Status = ServerStatus.Running;
            return;
        }

        var portOutput = await RunAsync(client,
            $"(ss -ltnp 2>/dev/null || netstat -ltnp 2>/dev/null || true) | grep ':{server.Port} ' || true",
            cancellationToken);

        server.Status = string.IsNullOrWhiteSpace(portOutput) ? ServerStatus.Stopped : ServerStatus.Running;
    }

    private static async Task<bool> FileExistsAsync(SshClient client, string path, CancellationToken cancellationToken)
    {
        var output = await RunAsync(client, $"test -x {Quote(path)} && echo yes || echo no", cancellationToken);
        return output.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T> WithClientAsync<T>(AppConfig config, Func<SshClient, Task<T>> action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            throw new InvalidOperationException("SSH host is empty.");
        }

        if (string.IsNullOrWhiteSpace(config.Username))
        {
            throw new InvalidOperationException("SSH username is empty.");
        }

        var connection = new ConnectionInfo(
            config.Host,
            config.SshPort,
            config.Username,
            new PasswordAuthenticationMethod(config.Username, config.Password ?? string.Empty))
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        using var client = new SshClient(connection);
        await Task.Run(() => client.Connect(), cancellationToken);
        try
        {
            return await action(client);
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    private static async Task<string> RunAsync(SshClient client, string command, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sshCommand = client.CreateCommand(command);
            sshCommand.CommandTimeout = TimeSpan.FromSeconds(12);
            var output = sshCommand.Execute();
            var combined = new StringBuilder(output);
            if (!string.IsNullOrWhiteSpace(sshCommand.Error))
            {
                combined.AppendLine(sshCommand.Error);
            }

            return combined.ToString();
        }, cancellationToken);
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}
