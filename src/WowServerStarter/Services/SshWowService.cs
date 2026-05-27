using System.Text;
using Renci.SshNet;
using WowServerStarter.Models;

namespace WowServerStarter.Services;

public sealed class SshWowService
{
    private static readonly ServerEntry[] KnownServers =
    [
        new()
        {
            Id = "azeroth-auth",
            Name = "AzerothCore Authserver",
            Type = ServerType.AuthServer,
            RootPath = "/opt/azeroth-server",
            BinaryName = "authserver",
            Port = 3724
        },
        new()
        {
            Id = "azeroth-world",
            Name = "Realm 1 Worldserver",
            Type = ServerType.WorldServer,
            RootPath = "/opt/azeroth-server",
            BinaryName = "worldserver",
            Port = 8085
        },
        new()
        {
            Id = "playerbots-world",
            Name = "Playerbots Realm Worldserver",
            Type = ServerType.WorldServer,
            RootPath = "/opt/azeroth-playerbots-server",
            BinaryName = "worldserver",
            Port = 8086
        }
    ];

    public async Task<IReadOnlyList<ServerEntry>> DiscoverAsync(AppConfig config, CancellationToken cancellationToken)
    {
        return await WithClientAsync(config, async client =>
        {
            var result = new List<ServerEntry>();
            foreach (var known in KnownServers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await FileExistsAsync(client, known.BinaryPath, cancellationToken))
                {
                    var server = new ServerEntry
                    {
                        Id = known.Id,
                        Name = known.Name,
                        Type = known.Type,
                        RootPath = known.RootPath,
                        BinaryName = known.BinaryName,
                        Port = known.Port
                    };
                    await RefreshStatusAsync(client, server, cancellationToken);
                    result.Add(server);
                }
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
            await RunAsync(client, BuildStartCommand(server), cancellationToken, "Startbefehl fehlgeschlagen");
            await WaitForStatusAsync(client, server, ServerStatus.Running, TimeSpan.FromSeconds(12), cancellationToken);

            if (server.Status != ServerStatus.Running)
            {
                throw new InvalidOperationException("Startbefehl ausgeführt, Prozess läuft nicht");
            }

            return true;
        }, cancellationToken);
    }

    public async Task StopAsync(AppConfig config, ServerEntry server, CancellationToken cancellationToken)
    {
        await WithClientAsync(config, async client =>
        {
            await RunAsync(client, BuildKillCommand(server, "TERM"), cancellationToken, "Stopbefehl fehlgeschlagen");
            await WaitForStatusAsync(client, server, ServerStatus.Stopped, TimeSpan.FromSeconds(4), cancellationToken);

            if (server.Status == ServerStatus.Running)
            {
                await RunAsync(client, BuildKillCommand(server, "KILL"), cancellationToken, "Stopbefehl fehlgeschlagen");
                await WaitForStatusAsync(client, server, ServerStatus.Stopped, TimeSpan.FromSeconds(3), cancellationToken);
            }

            return true;
        }, cancellationToken);
    }

    public async Task RebootAsync(AppConfig config, ServerEntry server, CancellationToken cancellationToken)
    {
        await StopAsync(config, server, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        await StartAsync(config, server, cancellationToken);
    }

    private static async Task RefreshStatusAsync(SshClient client, ServerEntry server, CancellationToken cancellationToken)
    {
        var processOutput = await RunAsync(client, BuildFindPidCommand(server), cancellationToken, throwOnFailure: false);
        server.ProcessId = TryReadPid(processOutput);

        if (server.ProcessId.HasValue)
        {
            server.Status = ServerStatus.Running;
            return;
        }

        var portOutput = await RunAsync(client,
            $"(ss -tulpen 2>/dev/null || netstat -tulpen 2>/dev/null || true) | grep -E ':{server.Port}([^0-9]|$)' || true",
            cancellationToken,
            throwOnFailure: false);

        server.Status = string.IsNullOrWhiteSpace(portOutput) ? ServerStatus.Stopped : ServerStatus.Running;
    }

    private static async Task WaitForStatusAsync(
        SshClient client,
        ServerEntry server,
        ServerStatus expectedStatus,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopAt = DateTimeOffset.UtcNow.Add(timeout);
        do
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            await RefreshStatusAsync(client, server, cancellationToken);
            if (server.Status == expectedStatus)
            {
                return;
            }
        }
        while (DateTimeOffset.UtcNow < stopAt);
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
        try
        {
            await Task.Run(() => client.Connect(), cancellationToken);
        }
        catch (Renci.SshNet.Common.SshAuthenticationException ex)
        {
            throw new InvalidOperationException("Passwort falsch oder SSH-Login abgelehnt", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Verbindung fehlgeschlagen", ex);
        }

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

    private static async Task<string> RunAsync(
        SshClient client,
        string command,
        CancellationToken cancellationToken,
        string failureMessage = "SSH-Befehl fehlgeschlagen",
        bool throwOnFailure = true)
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

            if (throwOnFailure && sshCommand.ExitStatus != 0)
            {
                throw new InvalidOperationException(failureMessage);
            }

            return combined.ToString();
        }, cancellationToken);
    }

    private static int? TryReadPid(string? processLine)
    {
        if (string.IsNullOrWhiteSpace(processLine))
        {
            return null;
        }

        var firstPart = processLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(firstPart, out var pid) ? pid : null;
    }

    private static string BuildStartCommand(ServerEntry server)
    {
        var logTarget = $"../logs/{server.LogFileName}";
        var startWithLogs = "if command -v setsid >/dev/null 2>&1; then "
            + "setsid nohup ./" + server.BinaryName + " > " + Quote(logTarget) + " 2>&1 < /dev/null & "
            + "else nohup ./" + server.BinaryName + " > " + Quote(logTarget) + " 2>&1 < /dev/null & fi";
        var startWithoutLogs = "if command -v setsid >/dev/null 2>&1; then "
            + "setsid nohup ./" + server.BinaryName + " > /dev/null 2>&1 < /dev/null & "
            + "else nohup ./" + server.BinaryName + " > /dev/null 2>&1 < /dev/null & fi";

        return "cd " + Quote(server.BinPath)
            + " && if [ -d ../logs ] && [ -w ../logs ]; then "
            + startWithLogs
            + "; else "
            + startWithoutLogs
            + "; fi";
    }

    private static string BuildFindPidCommand(ServerEntry server)
    {
        return "for pid in $(pgrep -x " + Quote(server.BinaryName) + " 2>/dev/null || true); do "
            + "exe=$(readlink -f /proc/$pid/exe 2>/dev/null || true); "
            + "cmd=$(tr '\\0' ' ' < /proc/$pid/cmdline 2>/dev/null || true); "
            + "if [ \"$exe\" = " + Quote(server.BinaryPath) + " ] || printf '%s' \"$cmd\" | grep -F " + Quote(server.BinaryPath) + " >/dev/null; then "
            + "echo $pid; "
            + "fi; "
            + "done | head -n 1";
    }

    private static string BuildKillCommand(ServerEntry server, string signal)
    {
        return "for pid in $(pgrep -x " + Quote(server.BinaryName) + " 2>/dev/null || true); do "
            + "exe=$(readlink -f /proc/$pid/exe 2>/dev/null || true); "
            + "cmd=$(tr '\\0' ' ' < /proc/$pid/cmdline 2>/dev/null || true); "
            + "if [ \"$exe\" = " + Quote(server.BinaryPath) + " ] || printf '%s' \"$cmd\" | grep -F " + Quote(server.BinaryPath) + " >/dev/null; then "
            + "kill -" + signal + " $pid 2>/dev/null || true; "
            + "fi; "
            + "done; true";
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}
