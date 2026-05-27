using Avalonia.Threading;

namespace WowServerStarter.Services;

public sealed class SoundService
{
    public void Ping(bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                Console.Beep(880, 70);
            }
            catch
            {
                // Some macOS/sandboxed terminals do not expose a console bell.
            }
        });
    }
}
