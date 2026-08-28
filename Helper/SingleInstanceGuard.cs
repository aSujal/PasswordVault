using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordVault.Helper;

// Ensures only one PasswordVault process runs at a time. A second launch notifies
// the already-running instance (via a named pipe) to come to the foreground, then exits
// immediately instead of opening a competing window onto the same encrypted vault file.
public static class SingleInstanceGuard
{
    private const string MutexName = "PasswordVault-SingleInstance-6F3E1B2A";
    private const string PipeName = "PasswordVault-Activate-6F3E1B2A";

    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        return createdNew;
    }

    public static void NotifyRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("activate");
        }
        catch
        {
            // The running instance isn't responding to the pipe; nothing more we can do.
        }
    }

    public static void StartActivationListener(Action onActivateRequested)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    onActivateRequested();
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
        });
    }
}
