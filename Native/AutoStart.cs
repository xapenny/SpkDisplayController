using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SpkDisplayController.Native;

/// <summary>
/// Launch at startup via Windows Task Scheduler.
/// </summary>
/// <remarks>
/// The app manifest declares requireAdministrator; launching via HKCU\...\Run
/// would trigger a UAC prompt on every login. Instead, register a Task Scheduler
/// entry with InteractiveToken + HighestAvailable via the Schedule.Service COM API
/// so Task Scheduler launches it silently after logon with an elevated token.
/// </remarks>
internal static class AutoStart
{
    private const string TaskName = "SpkDisplayController";

    private const int TaskCreateOrUpdate = 6;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskRunLevelHighest = 1;
    private const int TaskTriggerLogon = 9;
    private const int TaskActionExec = 0;
    private const int TaskInstancesIgnoreNew = 2;

    public static string? LastError { get; private set; }

    public static bool IsEnabled => TaskIsEnabledForCurrentExe();

    public static bool Set(bool enabled) => enabled ? Enable() : Disable();

    public static bool Enable()
    {
        LastError = null;
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            LastError = "Executable path is unavailable.";
            return false;
        }

        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType is null)
            {
                LastError = "Task Scheduler COM service is unavailable.";
                return false;
            }

            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();

            dynamic folder = service.GetFolder(@"\");
            dynamic task = service.NewTask(0);

            using var identity = WindowsIdentity.GetCurrent();
            var userId = identity.User?.Value ?? identity.Name;

            task.RegistrationInfo.Description = "Start SpkDisplayController automatically at Windows logon.";

            task.Principal.UserId = userId;
            task.Principal.LogonType = TaskLogonInteractiveToken;
            task.Principal.RunLevel = TaskRunLevelHighest;

            task.Settings.Enabled = true;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.ExecutionTimeLimit = "PT0S";
            task.Settings.MultipleInstances = TaskInstancesIgnoreNew;
            task.Settings.StartWhenAvailable = true;
            task.Settings.IdleSettings.StopOnIdleEnd = false;

            dynamic trigger = task.Triggers.Create(TaskTriggerLogon);
            trigger.Id = "LogonTrigger";
            trigger.UserId = userId;
            trigger.Delay = "PT5S";
            trigger.Enabled = true;

            dynamic action = task.Actions.Create(TaskActionExec);
            action.Path = exe;
            var workingDir = Path.GetDirectoryName(exe);
            if (!string.IsNullOrEmpty(workingDir))
                action.WorkingDirectory = workingDir;

            folder.RegisterTaskDefinition(
                TaskName,
                task,
                TaskCreateOrUpdate,
                null,
                null,
                TaskLogonInteractiveToken);

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public static bool Disable()
    {
        LastError = null;

        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType is null)
                return false;

            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();

            dynamic folder = service.GetFolder(@"\");
            folder.DeleteTask(TaskName, 0);
            return true;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (COMException ex) when (unchecked((uint)ex.HResult) == 0x80070002)
        {
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private static bool TaskIsEnabledForCurrentExe()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExe))
            return false;

        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType is null)
                return false;

            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();

            dynamic folder = service.GetFolder(@"\");
            dynamic registered = folder.GetTask(TaskName);
            if (!(bool)registered.Enabled)
                return false;

            foreach (dynamic action in registered.Definition.Actions)
            {
                if ((int)action.Type != TaskActionExec)
                    continue;

                var path = ((string?)action.Path)?.Trim().Trim('"');
                if (string.Equals(path, currentExe, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
