using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.Storage;

namespace NewsroomClocks;

/// <summary>
/// App logic that runs even if there's no UI
/// </summary>
internal class Manager : INotifyPropertyChanged
{
    static internal Manager? Instance;

    // Settings names
    private const string HoursOnlySettingKey = "HoursOnlySetting";
    private const string TimeZonesSettingKey = "SavedTimeZones";

    // The icons being display in the systray
    internal ObservableCollection<TimeNotifyIcon> TimeNotifyIcons => _timeNotifyIcons;
    internal bool ZoneListIsEmpty => _timeNotifyIcons == null || !_timeNotifyIcons.Any();

    internal bool HasTimezones => _timeNotifyIcons != null && _timeNotifyIcons.Count > 0;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer _updateIconsTimer;
    private bool _timerIntervalNeedsReset = false;
    private ObservableCollection<TimeNotifyIcon> _timeNotifyIcons = new();
    private bool _isLaunchOnStartupEnabled = false;
    private bool _hoursOnly = false;

    /// <summary>
    /// Call private constructor and initialize static Instance property
    /// </summary>
    internal static void EnsureCreated()
    {
        if (Instance == null)
        {
            _ = new Manager(); // static Instance set in constructor
        }
    }

    private Manager()
    {
        Instance = this;

        // Saved settings like what time zones and how to display
        LoadAppSetting();

        // Create a dispatcher timer, which needs a DispatcherQueue
        // When the App starts it will create a DispatcherQueue too, and we'll switch to taht
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _updateIconsTimer = dispatcherQueue.CreateTimer();

        _updateIconsTimer.Tick += Timer_Tick;

        // Initialize the timer to one once a minute, which is when the time gets updated on the notify icon
        // When the device is suspended (sleeps), the timer will tick right away on startup
        // (or as soon as it can get CPU time), not complete what was remaining of the minute when the sleep started
        _updateIconsTimer.Interval = TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(DateTime.Now.Second);

        _updateIconsTimer.Start();

        // Set initial time in the notify icon to now
        UpdateTrayIcons();
    }

    /// <summary>
    /// If set means to only show hours, not hours:minutes
    /// </summary>
    internal bool HoursOnly
    {
        get => _hoursOnly;
        set
        {
            if (_hoursOnly != value)
            {
                _hoursOnly = value;
                RaisePropertyChanged();
                UpdateTrayIcons();

                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[HoursOnlySettingKey] = _hoursOnly;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// If set, and a timezone has bee selected, launch app on system startup automatically
    /// </summary>
    internal bool IsLaunchOnStartupEnabled
    {
        get => _isLaunchOnStartupEnabled;
        set
        {
            if (_isLaunchOnStartupEnabled != value)
            {
                _isLaunchOnStartupEnabled = value;
                RaisePropertyChanged();
                UpdateStartupInSystem();
            }
        }
    }

    /// <summary>
    /// Call the Windows API to update the startup setting
    /// </summary>
    async void UpdateStartupInSystem()
    {
        var succeeded = await StartupManager.SetStartupEnabled(IsLaunchOnStartupEnabled);
        if (!succeeded)
        {
            // Disabled by policy
            // bugbug: show a message to the user, disable the toggle
            _isLaunchOnStartupEnabled = false;
            RaisePropertyChanged(nameof(IsLaunchOnStartupEnabled));
        }
    }

    /// <summary>
    /// Load saved app settings from ApplicationDataContainer
    /// </summary>
    private async void LoadAppSetting()
    {
        var localSettings = ApplicationData.Current.LocalSettings;

        // HoursOnly setting
        _hoursOnly = false;
        if (localSettings.Values.ContainsKey(HoursOnlySettingKey))
        {
            _hoursOnly = localSettings.Values[HoursOnlySettingKey] as bool? ?? false;
        }

        // Saved time zones
        LoadTimeZonesSettings();

        // LaunchOnStartup setting
        _isLaunchOnStartupEnabled = await StartupManager.IsStartupEnabledAsync();
        RaisePropertyChanged(nameof(IsLaunchOnStartupEnabled));
    }

    /// <summary>
    /// Update notify icons on timer tick
    /// </summary>
    private void Timer_Tick(object? sender, object e)
    {
        Debug.WriteLine($"Tick {DateTime.Now}");
        TimeSpan? newInterval = null;

        try
        {
            var now = DateTime.Now;
            if (now.Second != 0)
            {
                // Out of sync with the system clock, reset timer interval to sync with the next minute
                //_updateIconsTimer.Interval = TimeSpan.FromSeconds(60 - now.Second);
                newInterval = TimeSpan.FromSeconds(60 - now.Second);
                _timerIntervalNeedsReset = true;
            }
            else if (_timerIntervalNeedsReset)
            {
                // In sync, but we were out of sync and set the timer to something other than 60s
                // Set it back to 60s
                _timerIntervalNeedsReset = false;
                newInterval = TimeSpan.FromMinutes(1);
            }

            if (newInterval != null)
            {
                try
                {
                    _updateIconsTimer.Stop();
                    _updateIconsTimer.Interval = newInterval.Value;
                }
                finally
                {
                    _updateIconsTimer.Start();
                }
            }

            // Update the icons in the sys tray, also the window if it's open
            UpdateTrayIcons();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception in Timer_Tick: {ex}");

            // Sometimes, very infrequently, this timer stops ticking, IsRunning goes false, and can't figure out why.
            // So adding this try/catch in case an exception is causing it
            if (!_updateIconsTimer.IsRunning)
            {
                // Ensure the timer is running
                _updateIconsTimer.Start();
            }
        }
    }

    /// <summary>
    /// Set the icons in the systray to the current time
    /// </summary>
    private void UpdateTrayIcons()
    {
        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.UpdateForCurrentTime();
        }
    }

    /// <summary>
    /// Save to app settings the list of time zones that have been selected
    /// </summary>
    internal void SaveTimeZoneSettings()
    {
        RaisePropertyChanged(nameof(ZoneListIsEmpty));

        var localSettings = ApplicationData.Current.LocalSettings;
        var timeZoneList = _timeNotifyIcons
            .Select(icon => $"{icon.TimeZone.Id}:{icon.CityName}")
            .ToArray();

        if (timeZoneList == null || timeZoneList.Length == 0)
        {
            if (localSettings.Values.ContainsKey(TimeZonesSettingKey))
            {
                localSettings.Values.Remove(TimeZonesSettingKey);
            }
            return;
        }
        else
        {
            localSettings.Values[TimeZonesSettingKey] = timeZoneList;
        }
    }

    /// <summary>
    /// Load from app settings the list of time zones to display
    /// </summary>
    internal void LoadTimeZonesSettings()
    {
        var localSettings = ApplicationData.Current.LocalSettings;

        if (localSettings.Values.ContainsKey(TimeZonesSettingKey))
        {
            var savedTimeZones = localSettings.Values[TimeZonesSettingKey] as string[];
            if (savedTimeZones != null && savedTimeZones.Length > 0)
            {
                foreach (var savedTimeZone in savedTimeZones)
                {
                    try
                    {
                        var parts = savedTimeZone.Split(":");
                        if (parts.Length != 2)
                        {
                            // Shouldn't ever happen
                            continue;
                        }

                        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(parts[0]);
                        var cityName = parts.Length > 1 ? parts[1] : string.Empty;
                        _timeNotifyIcons.Add(new TimeNotifyIcon(timeZone, cityName));

                    }
                    catch (TimeZoneNotFoundException)
                    {
                        // Shouldn't ever happen
                        continue;
                    }
                }

                // Property is a function of _timeNotifyIcons
                RaisePropertyChanged(nameof(ZoneListIsEmpty));
            }
        }
    }

    /// <summary>
    /// Add a time zone to display
    /// </summary>
    internal void AddTimeZone(TimeZoneInfo timeZone, string cityName)
    {
        _timeNotifyIcons.Add(new TimeNotifyIcon(timeZone, cityName));
        SaveTimeZoneSettings();
        UpdateTrayIcons();
    }

    /// <summary>
    /// Remove one of the time zones
    /// </summary>
    internal void RemoveTimeZone(TimeNotifyIcon timeNotifyIcon)
    {
        _timeNotifyIcons.Remove(timeNotifyIcon);
        timeNotifyIcon.Dispose();
        SaveTimeZoneSettings();
    }

    /// <summary>
    /// When selected by the user from the context menu, exit the application
    /// </summary>
    internal void ExitApplication()
    {
        _updateIconsTimer?.Stop();

        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.Dispose();
        }

        Environment.Exit(0);
    }
}
