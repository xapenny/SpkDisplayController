using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace SpkDisplayController.Cli;

/// <summary>
/// Named Pipe IPC between a running tray instance and CLI invocations.
/// </summary>
internal static class IpcBridge
{
    public const string PipeName = "SpkDisplayController.Ipc";

    public static bool TrySend(CliRequest request, out CliResponse response)
    {
        response = new CliResponse { ExitCode = 1, Stderr = "IPC communication failed." };

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.None);

            client.Connect(300);

            var utf8 = new UTF8Encoding(false);
            using var writer = new StreamWriter(client, utf8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, utf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            var json = JsonSerializer.Serialize(request, CliJsonContext.Default.CliRequest);
            writer.WriteLine(json);

            var line = reader.ReadLine();
            if (string.IsNullOrEmpty(line))
                return true;

            var parsed = JsonSerializer.Deserialize(line, CliJsonContext.Default.CliResponse);
            if (parsed is not null)
                response = parsed;

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception ex)
        {
            response = new CliResponse { ExitCode = 1, Stderr = ex.Message };
            return true;
        }
    }

    public sealed class Server : IDisposable
    {
        private readonly Func<CliRequest, CliResponse> _handler;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loopTask;
        private bool _disposed;

        public Server(Func<CliRequest, CliResponse> handler)
        {
            _handler = handler;
            _loopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = CreateServerStream();
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    await HandleClientAsync(server, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
                finally
                {
                    if (server is not null)
                        await server.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken token)
        {
            var utf8 = new UTF8Encoding(false);
            using var reader = new StreamReader(server, utf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            using var writer = new StreamWriter(server, utf8, leaveOpen: true) { AutoFlush = true };

            var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                return;

            CliResponse response;
            try
            {
                var request = JsonSerializer.Deserialize(line, CliJsonContext.Default.CliRequest);
                response = request is null
                    ? new CliResponse { ExitCode = 2, Stderr = "Invalid IPC request." }
                    : _handler(request);
            }
            catch (Exception ex)
            {
                response = new CliResponse { ExitCode = 1, Stderr = ex.Message };
            }

            var json = JsonSerializer.Serialize(response, CliJsonContext.Default.CliResponse);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
        }

        private static NamedPipeServerStream CreateServerStream()
        {
            var security = new PipeSecurity();
            using var identity = WindowsIdentity.GetCurrent();
            if (identity.User is not null)
            {
                security.AddAccessRule(new PipeAccessRule(
                    identity.User,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
            }

            return NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                security);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cts.Cancel();
            try
            {
                _loopTask.Wait(500);
            }
            catch
            {
            }
            _cts.Dispose();
        }
    }
}
