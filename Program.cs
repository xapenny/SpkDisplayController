using System.Text;
using SpkDisplayController.Localization;
using SpkDisplayController.Services;

namespace SpkDisplayController;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            TryWriteCrash(e.ExceptionObject?.ToString() ?? "unknown");
        };

        try
        {
            var langArg = args.FirstOrDefault(a =>
                a.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase));
            if (langArg is not null)
            {
                var code = langArg["--lang=".Length..];
                try
                {
                    var culture = new System.Globalization.CultureInfo(code);
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

            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                RunSelfTest();
                return;
            }

            if (args.Any(a => string.Equals(a, "--dumpmenu", StringComparison.OrdinalIgnoreCase)))
            {
                var simulate = args.Any(a => string.Equals(a, "--simulate-softoff", StringComparison.OrdinalIgnoreCase));
                RunMenuDump(simulate);
                return;
            }

            using var context = new TrayAppContext();
            Application.Run(context);
        }
        catch (Exception ex)
        {
            TryWriteCrash(ex.ToString());
        }
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
            using var context = new TrayAppContext();
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
