using System.Text;
using SpkDisplayController.Cli;
using SpkDisplayController.Localization;
using SpkDisplayController.Native;
using SpkDisplayController.Services;

namespace SpkDisplayController;

internal static class Program
{
    private const string TrayMutexName = @"Local\SpkDisplayController.Tray";

    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            TryWriteCrash(e.ExceptionObject?.ToString() ?? "unknown");
        };

        try
        {
            if (CliParser.TryExtractLanguage(args, out var langCode) && !string.IsNullOrWhiteSpace(langCode))
            {
                try
                {
                    var culture = new System.Globalization.CultureInfo(langCode);
                    System.Globalization.CultureInfo.CurrentUICulture = culture;
                    System.Globalization.CultureInfo.CurrentCulture = culture;
                }
                catch
                {
                }
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (CliParser.HasFlag(args, "selftest"))
            {
                RunSelfTest();
                return 0;
            }

            if (CliParser.HasFlag(args, "dumpmenu"))
            {
                var simulate = CliParser.HasFlag(args, "simulate-softoff");
                RunMenuDump(simulate);
                return 0;
            }

            if (CliParser.HasCliArgs(args))
                return RunCli(args);

            using var instanceMutex = new Mutex(initiallyOwned: true, TrayMutexName, out var createdNew);
            if (!createdNew)
                return 0;

            using var context = new TrayAppContext();
            Application.Run(context);
            return 0;
        }
        catch (Exception ex)
        {
            TryWriteCrash(ex.ToString());
            return 1;
        }
    }

    private static int RunCli(string[] args)
    {
        var parsed = CliParser.Parse(args);
        if (parsed.ShowHelp)
        {
            ConsoleHelper.WriteOutput(CliParser.HelpText, null);
            return 0;
        }

        if (parsed.Error is not null || parsed.Request is null)
        {
            ConsoleHelper.WriteOutput(null, parsed.Error ?? "Invalid arguments.");
            return 2;
        }

        CliResponse response;
        if (!IpcBridge.TrySend(parsed.Request, out response))
        {
            Loc.Initialize();
            using var service = new MonitorService();
            service.Refresh();
            response = CliCommandHandler.Execute(service, parsed.Request);
        }

        ConsoleHelper.WriteOutput(response.Stdout, response.Stderr);
        return response.ExitCode;
    }

    // Note: In some restricted environments %TEMP% is not writable by child processes, so logs are written to the app directory.
    private static string LogPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, fileName);

    private static void TryWriteCrash(string text)
    {
        try
        {
            File.WriteAllText(LogPath("SpkDisplayController-crash.txt"), text, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static void RunMenuDump(bool simulateSoftOff = false)
    {
        var sb = new StringBuilder();

        try
        {
            using var context = new TrayAppContext(enableIpc: false);
            Loc.Initialize();

            if (simulateSoftOff)
                context.SimulateSoftOff();

            sb.AppendLine($"Language : {Loc.LanguageCode}");
            if (simulateSoftOff)
                sb.AppendLine("Mode     : simulated soft-off");
            sb.AppendLine();
            sb.AppendLine(context.DumpMenuTree());
        }
        catch (Exception ex)
        {
            sb.AppendLine("EXCEPTION:");
            sb.AppendLine(ex.ToString());
        }

        try
        {
            File.WriteAllText(LogPath("SpkDisplayController-menu.txt"), sb.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static void RunSelfTest()
    {
        var sb = new StringBuilder();

        try
        {
            Loc.Initialize();
            sb.AppendLine($"Language : {Loc.LanguageCode}");
            sb.AppendLine($"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"Runtime  : {Environment.Version}");

            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                var elevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                sb.AppendLine($"Elevated : {elevated}");
                sb.AppendLine($"User     : {identity.Name}");
            }

            sb.AppendLine();

            using var service = new MonitorService();
            service.Refresh();

            sb.AppendLine($"Monitors : {service.Monitors.Count}");

            foreach (var m in service.Monitors)
            {
                sb.AppendLine("----------------------------------------------");
                sb.AppendLine($"  DeviceName   : {m.DeviceName}");
                sb.AppendLine($"  DisplayLabel : {m.DisplayLabel}");
                sb.AppendLine($"  Description  : {m.Description}");
                sb.AppendLine($"  Manufacturer : {m.Manufacturer}");
                sb.AppendLine($"  DeviceId     : {m.DeviceId}");
                sb.AppendLine($"  Target       : {(m.Target is null ? "<null>" : $"id={m.Target.TargetId} tech={m.Target.OutputTechnology} path={m.Target.NormalizedPath}")}");
                sb.AppendLine($"  Brightness   : {(m.SupportsBrightness ? $"{m.Brightness}/{m.BrightnessMax} = {m.BrightnessPercent}%" : "<unsupported>")}");
                sb.AppendLine($"  Power        : {(m.SupportsPower ? $"state={m.PowerState} softOff={m.IsSoftOff}" : "<unsupported>")}");
                sb.AppendLine($"  HDR          : {(m.SupportsHdr ? (m.HdrEnabled ? "enabled" : "disabled") : "<unsupported>")}");
                sb.AppendLine($"  DisplayMode  : {(m.SupportsDisplayModes ? $"{m.CurrentWidth}x{m.CurrentHeight} @{m.CurrentRefreshRate}Hz orient={m.CurrentOrientation} ({m.SupportedResolutions.Count} res, {m.SupportedRefreshRates.Count} hz)" : "<unsupported>")}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("EXCEPTION:");
            sb.AppendLine(ex.ToString());
        }

        try
        {
            File.WriteAllText(LogPath("SpkDisplayController-selftest.txt"), sb.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
    }
}
