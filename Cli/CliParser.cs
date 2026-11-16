using System.Text.Json.Serialization;

namespace SpkDisplayController.Cli;

internal sealed class CliRequest
{
    public bool List { get; set; }
    public bool Json { get; set; }
    public bool RefreshMonitors { get; set; }
    public string? Monitor { get; set; }
    public string? Hdr { get; set; }
    public string? Brightness { get; set; }
    public string? Resolution { get; set; }
    public string? RefreshRate { get; set; }
    public string? Orientation { get; set; }
    public bool SoftOff { get; set; }

    [JsonIgnore]
    public bool HasMonitorAction =>
        Hdr is not null
        || Brightness is not null
        || Resolution is not null
        || RefreshRate is not null
        || Orientation is not null
        || SoftOff;
}

internal sealed class CliResponse
{
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
}

internal sealed class MonitorInfoDto
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HdrSupported { get; set; }
    public bool HdrEnabled { get; set; }
    public bool BrightnessSupported { get; set; }
    public int? BrightnessPercent { get; set; }
    public bool PowerSupported { get; set; }
    public bool SoftOff { get; set; }
    public bool DisplayModesSupported { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? RefreshRate { get; set; }
    public int? Orientation { get; set; }
    public string? OrientationName { get; set; }
    public string[] SupportedResolutions { get; set; } = Array.Empty<string>();
    public int[] SupportedRefreshRates { get; set; } = Array.Empty<int>();
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CliRequest))]
[JsonSerializable(typeof(CliResponse))]
[JsonSerializable(typeof(List<MonitorInfoDto>))]
internal partial class CliJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<MonitorInfoDto>))]
internal partial class CliPrettyJsonContext : JsonSerializerContext
{
}

internal static class CliParser
{
    public readonly record struct ParseResult(bool ShowHelp, CliRequest? Request, string? Error);

    public static bool HasCliArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsLangFlag(arg, out var hasInlineValue))
            {
                if (!hasInlineValue && i + 1 < args.Length)
                    i++;
                continue;
            }

            return true;
        }

        return false;
    }

    public static bool TryExtractLanguage(string[] args, out string? langCode)
    {
        langCode = null;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!IsLangFlag(arg, out var hasInlineValue))
                continue;

            if (hasInlineValue)
            {
                var eq = arg.IndexOf('=');
                langCode = arg[(eq + 1)..];
                return true;
            }

            if (i + 1 < args.Length)
            {
                langCode = args[i + 1];
                return true;
            }
        }

        return false;
    }

    public static bool HasFlag(string[] args, string longName)
    {
        foreach (var arg in args)
        {
            var name = TrimFlagPrefix(arg);
            if (name is not null && string.Equals(name, longName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static ParseResult Parse(string[] args)
    {
        var request = new CliRequest();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsLangFlag(arg, out var hasInlineLang))
            {
                if (!hasInlineLang)
                {
                    if (i + 1 >= args.Length)
                        return new ParseResult(false, null, $"Missing value after {arg}.");
                    i++;
                }
                continue;
            }

            if (arg == "/?")
                return new ParseResult(true, null, null);

            if (!arg.StartsWith('-'))
            {
                switch (arg.ToLowerInvariant())
                {
                    case "help":
                        return new ParseResult(true, null, null);
                    case "list" or "ls" or "status":
                        request.List = true;
                        continue;
                    case "refresh-monitors":
                        request.RefreshMonitors = true;
                        continue;
                    default:
                        return new ParseResult(false, null, $"Unexpected argument '{arg}'. Run with --help or -h for usage.");
                }
            }

            var raw = TrimFlagPrefix(arg)!;
            var eqIndex = raw.IndexOf('=');
            var key = eqIndex >= 0 ? raw[..eqIndex] : raw;
            var inlineValue = eqIndex >= 0 ? raw[(eqIndex + 1)..] : null;

            string? ReadValue(out string? error)
            {
                if (inlineValue is not null)
                {
                    if (inlineValue.Length == 0)
                    {
                        error = $"Missing value for {arg}.";
                        return null;
                    }
                    error = null;
                    return inlineValue;
                }

                if (i + 1 >= args.Length)
                {
                    error = $"Missing value after {arg}.";
                    return null;
                }

                error = null;
                return args[++i];
            }

            bool EnsureNoInlineValue(out string? error)
            {
                if (inlineValue is not null)
                {
                    error = $"Flag '{key}' does not take a value.";
                    return false;
                }
                error = null;
                return true;
            }

            // Case-sensitive handling for single-letter collisions: -h vs -H, -r vs -R.
            if (key == "h")
            {
                if (!EnsureNoInlineValue(out var err)) return new ParseResult(false, null, err);
                return new ParseResult(true, null, null);
            }

            if (key == "H")
            {
                request.Hdr = ReadValue(out var err);
                if (err is not null) return new ParseResult(false, null, err);
                continue;
            }

            if (key == "r")
            {
                request.Resolution = ReadValue(out var err);
                if (err is not null) return new ParseResult(false, null, err);
                continue;
            }

            if (key == "R")
            {
                if (!EnsureNoInlineValue(out var err)) return new ParseResult(false, null, err);
                request.RefreshMonitors = true;
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "help":
                    if (!EnsureNoInlineValue(out var helpErr)) return new ParseResult(false, null, helpErr);
                    return new ParseResult(true, null, null);

                case "l" or "list":
                    if (!EnsureNoInlineValue(out var listErr)) return new ParseResult(false, null, listErr);
                    request.List = true;
                    break;

                case "j" or "json":
                    if (!EnsureNoInlineValue(out var jsonErr)) return new ParseResult(false, null, jsonErr);
                    request.Json = true;
                    break;

                case "m" or "monitor":
                    request.Monitor = ReadValue(out var monErr);
                    if (monErr is not null) return new ParseResult(false, null, monErr);
                    break;

                case "hdr":
                    request.Hdr = ReadValue(out var hdrErr);
                    if (hdrErr is not null) return new ParseResult(false, null, hdrErr);
                    break;

                case "b" or "brightness":
                    request.Brightness = ReadValue(out var briErr);
                    if (briErr is not null) return new ParseResult(false, null, briErr);
                    break;

                case "resolution":
                    request.Resolution = ReadValue(out var resErr);
                    if (resErr is not null) return new ParseResult(false, null, resErr);
                    break;

                case "f" or "refresh-rate" or "refresh" or "hz":
                    request.RefreshRate = ReadValue(out var hzErr);
                    if (hzErr is not null) return new ParseResult(false, null, hzErr);
                    break;

                case "o" or "orientation":
                    request.Orientation = ReadValue(out var oriErr);
                    if (oriErr is not null) return new ParseResult(false, null, oriErr);
                    break;

                case "s" or "soft-off" or "softoff":
                    if (!EnsureNoInlineValue(out var offErr)) return new ParseResult(false, null, offErr);
                    request.SoftOff = true;
                    break;

                case "refresh-monitors":
                    if (!EnsureNoInlineValue(out var refErr)) return new ParseResult(false, null, refErr);
                    request.RefreshMonitors = true;
                    break;

                default:
                    return new ParseResult(false, null, $"Unknown option: {arg}. Run with --help or -h for usage.");
            }
        }

        if (request.HasMonitorAction && string.IsNullOrWhiteSpace(request.Monitor))
            return new ParseResult(false, null, "Missing --monitor (-m) target. Run '--list' to see available monitors.");

        if (!request.HasMonitorAction && !request.List && !request.RefreshMonitors)
        {
            if (request.Monitor is not null || request.Json)
                request.List = true;
            else
                return new ParseResult(true, null, null);
        }

        return new ParseResult(false, request, null);
    }

    private static bool IsLangFlag(string arg, out bool hasInlineValue)
    {
        hasInlineValue = false;
        var raw = TrimFlagPrefix(arg);
        if (raw is null)
            return false;

        if (string.Equals(raw, "lang", StringComparison.OrdinalIgnoreCase))
            return true;

        if (raw.StartsWith("lang=", StringComparison.OrdinalIgnoreCase))
        {
            hasInlineValue = true;
            return true;
        }

        return false;
    }

    private static string? TrimFlagPrefix(string arg)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2)
            return arg[2..];
        if (arg.StartsWith('-') && arg.Length > 1)
            return arg[1..];
        return null;
    }

    public static string HelpText =>
        """
        SpkDisplayController CLI

        Usage:
          SpkDisplayController.exe [options]

        Every long option accepts both '--' and '-' (e.g. --resolution or -resolution).

        Options:
          -m, --monitor <target>           Target monitor: 1..N, DISPLAYn, name substring, or 'all'
          -l, --list (or 'list')           List monitors and current state
          -j, --json                       Output JSON with --list
          -r, --resolution <WxH>           Set resolution (e.g. 1920x1080)
          -f, --refresh-rate <Hz>          Set refresh rate (aliases: --refresh, --hz; e.g. 60 or 60Hz)
          -o, --orientation <value>        Set orientation: 0|landscape, 90|portrait, 180|landscape-flipped, 270|portrait-flipped
          -H, --hdr <on|off|toggle>        Set or toggle HDR
          -b, --brightness <0-100>         Set DDC brightness percentage (0..100)
          -s, --soft-off                   Send DDC soft off (VCP 0xD6 = 4)
          -R, --refresh-monitors           Re-enumerate monitors on running tray instance
              --lang=<code>                Override language (en, zh-Hans, ja, ko, ru)
          -h, --help                       Show this help

        Diagnostics:
              --selftest                   Write monitor report to SpkDisplayController-selftest.txt
              --dumpmenu [--simulate-softoff]
                                           Write menu tree to SpkDisplayController-menu.txt

        Examples:
          SpkDisplayController.exe -l
          SpkDisplayController.exe -m 1 --resolution 1920x1080 --refresh-rate 60
          SpkDisplayController.exe -m 1 -r 2560x1440 -f 144 -H off -b 50
          SpkDisplayController.exe -m Philips -orientation portrait
        """;
}
