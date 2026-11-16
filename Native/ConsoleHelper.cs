using System.Runtime.InteropServices;
using System.Text;

namespace SpkDisplayController.Native;

/// <summary>
/// Attaches a WinExe process to its parent console so CLI output is visible in terminals.
/// </summary>
internal static class ConsoleHelper
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private static bool _attached;

    public static void EnsureAttached()
    {
        if (_attached)
            return;

        _attached = true;

        var needsStdout = !Console.IsOutputRedirected;
        var needsStderr = !Console.IsErrorRedirected;

        if (needsStdout || needsStderr)
            AttachConsole(AttachParentProcess);

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
        }

        if (needsStdout)
        {
            try
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
                {
                    AutoFlush = true
                });
            }
            catch
            {
            }
        }

        if (needsStderr)
        {
            try
            {
                Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false))
                {
                    AutoFlush = true
                });
            }
            catch
            {
            }
        }
    }

    public static void WriteOutput(string? stdout, string? stderr)
    {
        EnsureAttached();

        if (!string.IsNullOrEmpty(stdout))
        {
            Console.Out.Write(stdout.EndsWith('\n') ? stdout : stdout + Environment.NewLine);
            Console.Out.Flush();
        }

        if (!string.IsNullOrEmpty(stderr))
        {
            Console.Error.Write(stderr.EndsWith('\n') ? stderr : stderr + Environment.NewLine);
            Console.Error.Flush();
        }
    }
}
