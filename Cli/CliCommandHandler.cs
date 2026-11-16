using System.Globalization;
using System.Text;
using System.Text.Json;
using SpkDisplayController.Models;
using SpkDisplayController.Services;

namespace SpkDisplayController.Cli;

internal static class CliCommandHandler
{
    public static CliResponse Execute(MonitorService service, CliRequest request)
    {
        var outLines = new List<string>();
        var errLines = new List<string>();
        var hasExecutionFailure = false;

        if (request.RefreshMonitors)
        {
            service.Refresh();
            if (!request.HasMonitorAction && !request.List)
                return Ok($"Refreshed: {service.Monitors.Count} monitor(s) detected.");
        }

        if (request.HasMonitorAction)
        {
            if (!TryValidateActionArgs(request, out var parsed, out var validationError))
                return Error(2, validationError!);

            var resolved = ResolveMonitors(service, request.Monitor!);
            if (resolved.Error is not null)
                return resolved.Error;

            foreach (var monitor in resolved.Monitors)
            {
                var (ok, msgs, errs) = ApplyActionsToMonitor(service, monitor, parsed);
                outLines.AddRange(msgs);
                errLines.AddRange(errs);
                if (!ok)
                    hasExecutionFailure = true;
            }
        }

        if (request.List)
        {
            var listResponse = HandleList(service, request);
            if (listResponse.ExitCode != 0)
                return listResponse;

            if (!string.IsNullOrEmpty(listResponse.Stdout))
                outLines.Add(listResponse.Stdout);
        }

        return new CliResponse
        {
            ExitCode = hasExecutionFailure ? 1 : 0,
            Stdout = string.Join(Environment.NewLine, outLines),
            Stderr = string.Join(Environment.NewLine, errLines)
        };
    }

    private readonly record struct ParsedActions(
        int? Width,
        int? Height,
        int? RefreshRate,
        int? Orientation,
        string? HdrMode,
        int? BrightnessPercent,
        bool SoftOff);

    private static bool TryValidateActionArgs(CliRequest request, out ParsedActions parsed, out string? error)
    {
        parsed = default;
        error = null;

        int? width = null;
        int? height = null;
        if (request.Resolution is not null)
        {
            if (!TryParseResolution(request.Resolution, out var w, out var h))
            {
                error = $"Invalid resolution '{request.Resolution}'. Expected format <Width>x<Height>, e.g. 1920x1080.";
                return false;
            }
            width = w;
            height = h;
        }

        int? refreshRate = null;
        if (request.RefreshRate is not null)
        {
            var raw = request.RefreshRate.Trim();
            if (raw.EndsWith("hz", StringComparison.OrdinalIgnoreCase))
                raw = raw[..^2].Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hz) || hz <= 1)
            {
                error = $"Invalid refresh rate '{request.RefreshRate}'. Expected e.g. 60 or 60Hz.";
                return false;
            }
            refreshRate = hz;
        }

        int? orientation = null;
        if (request.Orientation is not null)
        {
            if (!TryParseOrientation(request.Orientation, out var o))
            {
                error = $"Invalid orientation '{request.Orientation}'. Expected: 0|landscape, 90|portrait, 180|landscape-flipped, or 270|portrait-flipped.";
                return false;
            }
            orientation = o;
        }

        string? hdrMode = null;
        if (request.Hdr is not null)
        {
            var val = request.Hdr.Trim().ToLowerInvariant();
            if (val is not ("on" or "off" or "toggle"))
            {
                error = $"Invalid HDR value '{request.Hdr}'. Expected: on, off, or toggle.";
                return false;
            }
            hdrMode = val;
        }

        int? brightness = null;
        if (request.Brightness is not null)
        {
            var raw = request.Brightness.Trim().TrimEnd('%');
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct)
                || pct < 0 || pct > 100)
            {
                error = $"Invalid brightness '{request.Brightness}'. Expected integer 0-100.";
                return false;
            }
            brightness = pct;
        }

        parsed = new ParsedActions(width, height, refreshRate, orientation, hdrMode, brightness, request.SoftOff);
        return true;
    }

    private static (bool Success, List<string> Out, List<string> Err) ApplyActionsToMonitor(
        MonitorService service, MonitorDevice monitor, ParsedActions actions)
    {
        var outLines = new List<string>();
        var errLines = new List<string>();
        var ok = true;

        service.ReadState(monitor);

        if (actions.Orientation is { } targetOrient)
        {
            if (!monitor.SupportsDisplayModes)
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: display modes are unavailable.");
            }
            else if (service.SetOrientation(monitor, targetOrient))
            {
                outLines.Add($"{monitor.DisplayLabel}: orientation {OrientationLabel(monitor.CurrentOrientation)} ({monitor.CurrentWidth}x{monitor.CurrentHeight})");
            }
            else
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: failed to set orientation.");
            }
        }

        if (actions.Width is not null || actions.RefreshRate is not null)
        {
            if (!monitor.SupportsDisplayModes)
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: display modes are unavailable.");
            }
            else
            {
                var targetW = actions.Width ?? monitor.CurrentWidth;
                var targetH = actions.Height ?? monitor.CurrentHeight;
                var modeValid = true;

                if (actions.Width is not null && !monitor.SupportedResolutions.Contains((targetW, targetH)))
                {
                    ok = false;
                    modeValid = false;
                    errLines.Add($"{monitor.DisplayLabel}: resolution {targetW}x{targetH} is not supported.");
                }

                if (modeValid && actions.RefreshRate is { } targetHz)
                {
                    var supportedRates = actions.Width is not null
                        ? service.GetSupportedRefreshRates(monitor, targetW, targetH)
                        : monitor.SupportedRefreshRates;

                    if (!supportedRates.Contains(targetHz))
                    {
                        ok = false;
                        modeValid = false;
                        var available = string.Join(", ", supportedRates.Select(r => $"{r}Hz"));
                        errLines.Add($"{monitor.DisplayLabel}: {targetHz}Hz is not supported at {targetW}x{targetH} (available: {available}).");
                    }
                }

                if (modeValid)
                {
                    if (service.SetDisplayMode(monitor, actions.Width, actions.Height, actions.RefreshRate))
                    {
                        outLines.Add($"{monitor.DisplayLabel}: mode {monitor.CurrentWidth}x{monitor.CurrentHeight} @{monitor.CurrentRefreshRate}Hz");
                    }
                    else
                    {
                        ok = false;
                        errLines.Add($"{monitor.DisplayLabel}: failed to change display mode.");
                    }
                }
            }
        }

        if (actions.HdrMode is { } hdrMode)
        {
            if (!monitor.SupportsHdr)
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: HDR is not supported.");
            }
            else
            {
                var targetHdr = hdrMode switch
                {
                    "on" => true,
                    "off" => false,
                    _ => !monitor.HdrEnabled
                };

                if (service.SetHdr(monitor, targetHdr))
                {
                    outLines.Add($"{monitor.DisplayLabel}: HDR {(targetHdr ? "on" : "off")}");
                }
                else
                {
                    ok = false;
                    errLines.Add($"{monitor.DisplayLabel}: failed to set HDR.");
                }
            }
        }

        if (actions.BrightnessPercent is { } targetBrightness)
        {
            if (!monitor.SupportsBrightness)
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: DDC brightness is not supported.");
            }
            else if (service.SetBrightnessPercent(monitor, targetBrightness))
            {
                outLines.Add($"{monitor.DisplayLabel}: brightness {targetBrightness}%");
            }
            else
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: failed to set brightness.");
            }
        }

        if (actions.SoftOff)
        {
            if (!monitor.SupportsPower)
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: DDC soft off is not supported.");
            }
            else if (monitor.IsSoftOff)
            {
                outLines.Add($"{monitor.DisplayLabel}: already soft off");
            }
            else if (service.SoftOff(monitor))
            {
                outLines.Add($"{monitor.DisplayLabel}: soft off sent");
            }
            else
            {
                ok = false;
                errLines.Add($"{monitor.DisplayLabel}: failed to soft off.");
            }
        }

        return (ok, outLines, errLines);
    }

    private static CliResponse HandleList(MonitorService service, CliRequest request)
    {
        service.ReadAll();

        List<MonitorDevice> targets;
        if (string.IsNullOrWhiteSpace(request.Monitor))
        {
            targets = service.Monitors.ToList();
        }
        else
        {
            var resolved = ResolveMonitors(service, request.Monitor);
            if (resolved.Error is not null)
                return resolved.Error;
            targets = resolved.Monitors;
        }

        if (request.Json)
        {
            var dtos = new List<MonitorInfoDto>(targets.Count);
            foreach (var monitor in targets)
            {
                var idx = IndexOfMonitor(service, monitor) + 1;
                dtos.Add(new MonitorInfoDto
                {
                    Index = idx,
                    DeviceName = monitor.DeviceName,
                    Label = monitor.DisplayLabel,
                    Description = monitor.Description,
                    HdrSupported = monitor.SupportsHdr,
                    HdrEnabled = monitor.HdrEnabled,
                    BrightnessSupported = monitor.SupportsBrightness,
                    BrightnessPercent = monitor.SupportsBrightness ? monitor.BrightnessPercent : null,
                    PowerSupported = monitor.SupportsPower,
                    SoftOff = monitor.SupportsPower && monitor.IsSoftOff,
                    DisplayModesSupported = monitor.SupportsDisplayModes,
                    Width = monitor.SupportsDisplayModes ? monitor.CurrentWidth : null,
                    Height = monitor.SupportsDisplayModes ? monitor.CurrentHeight : null,
                    RefreshRate = monitor.SupportsDisplayModes ? monitor.CurrentRefreshRate : null,
                    Orientation = monitor.SupportsDisplayModes ? monitor.CurrentOrientation : null,
                    OrientationName = monitor.SupportsDisplayModes ? OrientationLabel(monitor.CurrentOrientation) : null,
                    SupportedResolutions = monitor.SupportsDisplayModes
                        ? monitor.SupportedResolutions.Select(r => $"{r.Width}x{r.Height}").ToArray()
                        : Array.Empty<string>(),
                    SupportedRefreshRates = monitor.SupportsDisplayModes
                        ? monitor.SupportedRefreshRates.ToArray()
                        : Array.Empty<int>()
                });
            }

            var json = JsonSerializer.Serialize(dtos, CliPrettyJsonContext.Default.ListMonitorInfoDto);
            return Ok(json);
        }

        if (targets.Count == 0)
            return Ok("No monitors detected.");

        var sb = new StringBuilder();
        foreach (var m in targets)
        {
            var idx = IndexOfMonitor(service, m) + 1;
            var hdr = m.SupportsHdr ? (m.HdrEnabled ? "on" : "off") : "n/a";
            var bri = m.SupportsBrightness ? $"{m.BrightnessPercent}%" : "n/a";
            var mode = m.SupportsDisplayModes
                ? $"{m.CurrentWidth}x{m.CurrentHeight} @{m.CurrentRefreshRate}Hz ({OrientationLabel(m.CurrentOrientation)})"
                : "n/a";
            var power = m.SupportsPower ? (m.IsSoftOff ? "soft-off" : "on") : "n/a";

            sb.AppendLine($"[{idx}] {m.DisplayLabel} ({m.DeviceName})");
            sb.AppendLine($"    Mode       : {mode}");
            sb.AppendLine($"    HDR        : {hdr}");
            sb.AppendLine($"    Brightness : {bri}");
            sb.AppendLine($"    Power      : {power}");
        }

        return Ok(sb.ToString().TrimEnd());
    }

    private static (List<MonitorDevice> Monitors, CliResponse? Error) ResolveMonitors(
        MonitorService service, string target)
    {
        var monitors = service.Monitors;
        if (monitors.Count == 0)
            return (new List<MonitorDevice>(), Error(1, "No monitors detected."));

        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
            return (monitors.ToList(), null);

        if (int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            if (index < 1 || index > monitors.Count)
                return (new List<MonitorDevice>(), Error(2, $"Monitor index {index} out of range (1..{monitors.Count})."));

            return (new List<MonitorDevice> { monitors[index - 1] }, null);
        }

        var normalizedDevice = target.StartsWith(@"\\.\", StringComparison.Ordinal)
            ? target
            : target.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)
                ? @"\\.\" + target.ToUpperInvariant()
                : null;

        if (normalizedDevice is not null)
        {
            var byDevice = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, normalizedDevice, StringComparison.OrdinalIgnoreCase));
            if (byDevice is not null)
                return (new List<MonitorDevice> { byDevice }, null);
        }

        var matches = monitors.Where(m =>
            m.DisplayLabel.Contains(target, StringComparison.OrdinalIgnoreCase) ||
            m.Description.Contains(target, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 1)
            return (matches, null);

        if (matches.Count > 1)
        {
            var names = string.Join(", ", matches.Select(m => $"{m.DisplayLabel} ({m.DeviceName})"));
            return (new List<MonitorDevice>(), Error(2, $"Ambiguous monitor target '{target}'; matched: {names}"));
        }

        return (new List<MonitorDevice>(), Error(2, $"Monitor '{target}' not found. Run '--list' to see available monitors."));
    }

    private static int IndexOfMonitor(MonitorService service, MonitorDevice target)
    {
        for (var i = 0; i < service.Monitors.Count; i++)
        {
            if (ReferenceEquals(service.Monitors[i], target))
                return i;
        }
        return -1;
    }

    private static bool TryParseResolution(string? value, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Trim().Split(new[] { 'x', 'X', '×', '*' }, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
            && width > 0 && height > 0;
    }

    private static bool TryParseOrientation(string? value, out int orientation)
    {
        orientation = -1;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "0" or "landscape" or "default":
                orientation = 0;
                return true;
            case "90" or "portrait":
                orientation = 1;
                return true;
            case "180" or "landscape-flipped" or "landscapeflipped":
                orientation = 2;
                return true;
            case "270" or "portrait-flipped" or "portraitflipped":
                orientation = 3;
                return true;
            default:
                return false;
        }
    }

    private static string OrientationLabel(int orientation) => orientation switch
    {
        0 => "landscape (0)",
        1 => "portrait (90)",
        2 => "landscape-flipped (180)",
        3 => "portrait-flipped (270)",
        _ => orientation.ToString(CultureInfo.InvariantCulture)
    };

    private static CliResponse Ok(string stdout) => new() { ExitCode = 0, Stdout = stdout };
    private static CliResponse Error(int exitCode, string stderr) => new() { ExitCode = exitCode, Stderr = stderr };
}
