using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace TrayTime;

/// <summary>
/// Manages the application's auto-start behavior on Windows logon.
/// </summary>
internal static class StartupManager
{
    private const string TaskId = "TrayTimeStartupTask";

    /// <summary>
    /// Checks if the application is set to start automatically on Windows logon.
    /// </summary>
    /// <returns>True if auto-start is enabled, false otherwise.</returns>
    public static async Task<bool> IsStartupEnabledAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task?.State == StartupTaskState.Enabled;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables the application to start automatically on Windows logon.
    /// </summary>
    /// <param name="enable">True to enable auto-start, false to disable.</param>
    /// <returns>True if the operation succeeded, false otherwise.</returns>
    public static async Task<bool> SetStartupEnabled(bool enable)
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(TaskId);
            if (startupTask == null)
            {
                // Should never happen
                Debug.Assert(false);
                return false;
            }

            if (enable)
            {
                if (startupTask.State == StartupTaskState.Disabled)
                {
                    var result = await startupTask.RequestEnableAsync().AsTask();
                    return result == StartupTaskState.Enabled;
                }
                else if (startupTask.State == StartupTaskState.Enabled)
                {
                    return true;
                }
                else if (startupTask.State == StartupTaskState.DisabledByUser)
                {
                    // User has disabled this in Task Manager, we cannot enable it programmatically
                    return false;
                }
                else if (startupTask.State == StartupTaskState.DisabledByPolicy)
                {
                    // Disabled by group policy, we cannot enable it
                    return false;
                }
                return false;
            }
            else
            {
                if (startupTask.State == StartupTaskState.Enabled)
                {
                    startupTask.Disable();
                    return true;
                }
                return true; // Already disabled
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
}
