using System.Diagnostics;

namespace SpkDisplayController.Native;

/// <summary>
/// 开机自启。
/// </summary>
/// <remarks>
/// 程序清单声明了 requireAdministrator，若用 HKCU\...\Run 启动，
/// 每次登录都会弹出 UAC 提示。改用任务计划程序的 ONLOGON + HIGHEST，
/// 由任务计划服务持提升后的令牌，登录时静默启动，不弹 UAC。
/// </remarks>
internal static class AutoStart
{
    private const string TaskName = "SpkDisplayController";

    public static bool IsEnabled => TaskExists();

    public static void Set(bool enabled)
    {
        if (enabled)
            Enable();
        else
            Disable();
    }

    public static void Enable()
    {
        var exe = Environment.ProcessPath;

        if (string.IsNullOrEmpty(exe))
            return;

        // /TR 的值本身需要带引号，因此内层还要再转义一次
        var arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\" /SC ONLOGON /RL HIGHEST /F";
        RunSchtasks(arguments);
    }

    public static void Disable()
    {
        RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    private static bool TaskExists()
    {
        return RunSchtasks($"/Query /TN \"{TaskName}\"");
    }

    private static bool RunSchtasks(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);

            if (process is null)
                return false;

            // 必须排空输出，否则管道写满会卡住子进程
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(8000))
                return false;

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
