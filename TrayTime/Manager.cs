
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.Storage;

namespace TrayTime;

/// <summary>
/// App logic that runs even if there's no UI
/// </summary>
public class Manager : INotifyPropertyChanged
{
    static public Manager? Instance;

    // Settings names
    private const string HoursOnlySettingKey = "HoursOnlySetting";
    private const string TimeZonesSettingKey = "SavedTimeZones";

    // The icons being display in the systray
    public ObservableCollection<TimeNotifyIcon> TimeNotifyIcons => _timeNotifyIcons;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer _updateIconsTimer;
    private bool _timerIntervalNeedsReset = false;
    private ObservableCollection<TimeNotifyIcon> _timeNotifyIcons = new();
    private bool _isLaunchOnStartupEnabled = false;

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

        // Create a dispatcher timer
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _updateIconsTimer = dispatcherQueue.CreateTimer();

        // Initialize the timer to one once a minute, which is when the time gets updated on the notify icon
        _updateIconsTimer.Tick += Timer_Tick;
        _updateIconsTimer.Interval = TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(DateTime.Now.Second);
        _updateIconsTimer.Start();

        // Set initial time in the notify icon to now
        UpdateTrayIcons();

    }

    private bool _hoursOnly = false;
    public bool HoursOnly
    {
        get => _hoursOnly;
        set
        {
            if (_hoursOnly != value)
            {
                _hoursOnly = value;
                RaisePropertyChanged();

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

    public bool IsLaunchOnStartupEnabled
    {
        get => _isLaunchOnStartupEnabled;
        set
        {
            if (_isLaunchOnStartupEnabled != value)
            {
                _isLaunchOnStartupEnabled = value;
                RaisePropertyChanged();
                UpdateStartup();
            }
        }
    }

    async void UpdateStartup()
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

    private void Timer_Tick(object? sender, object e)
    {
        var now = DateTime.Now;
        if (now.Second != 0)
        {
            // Out of sync with the system clock, reset timer interval to sync with the next minute
            _updateIconsTimer.Stop();
            _updateIconsTimer.Interval = TimeSpan.FromSeconds(60 - now.Second);
            _updateIconsTimer.Start();
            _timerIntervalNeedsReset = true;
        }
        else if (_timerIntervalNeedsReset)
        {
            // In sync, but we were out of sync and set the timer to something other than 60s
            // Set it back to 60s
            _timerIntervalNeedsReset = false;
            _updateIconsTimer.Stop();
            _updateIconsTimer.Interval = TimeSpan.FromMinutes(1);
            _updateIconsTimer.Start();
        }

        // Update the icons in the sys tray, also the window if it's open
        UpdateTrayIcons();
    }

    private void UpdateTrayIcons()
    {
        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.UpdateForCurrentTime();
        }
    }

    public void SaveTimeZoneSettings()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        var timeZoneList = _timeNotifyIcons
            .Select(icon => $"{icon.TimeZone.Id}:{icon.CityName}")
            .ToArray();

        if (timeZoneList.Length == 0)
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

        //var timeZoneIds = _timeNotifyIcons.Select(icon => icon.TimeZone.Id).ToArray();

        // Save as comma-separated string
        //localSettings.Values[TimeZonesSettingKey] = string.Join(",", timeZoneIds);
    }

    public void LoadTimeZonesSettings()
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
            }
        }
    }

    public void AddTimeZone(TimeZoneInfo timeZone, string cityName)
    {
        _timeNotifyIcons.Add(new TimeNotifyIcon(timeZone, cityName));
        SaveTimeZoneSettings();
        UpdateTrayIcons();
    }

    public void RemoveTimeZone(TimeNotifyIcon timeNotifyIcon)
    {
        _timeNotifyIcons.Remove(timeNotifyIcon);
        timeNotifyIcon.Dispose();
        SaveTimeZoneSettings();
    }

    public void ExitApplication()
    {
        _updateIconsTimer?.Stop();

        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.Dispose();
        }

        Environment.Exit(0);
    }
}
