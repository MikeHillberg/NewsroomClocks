using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TrayTime;



public class Manager : INotifyPropertyChanged
{
    private const string HoursOnlySettingKey = "HoursOnlySetting";
    private const string TimeZonesSettingKey = "SavedTimeZones";

    private Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;
    private bool _updateTimer = false;
    private ObservableCollection<TimeNotifyIcon> _timeNotifyIcons = new();
    private bool _isStartupEnabled = false;

    static public Manager? Instance;

    public Manager()
    {
        Instance = this;

        // Load settings
        _hoursOnly = LoadHoursOnlySetting();

        // Load startup setting asynchronously
        _ = LoadStartupSettingAsync();

        // Initialize the timer to update the tray icon every minute
        // Use DispatcherQueueTimer which works with Application.Start()'s dispatcher
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(DateTime.Now.Second);
        _timer.Tick += Timer_Tick;


        // Load saved time zones
        LoadTimeZones();

        // Start timer after dispatcher queue is ready
        _timer.Start();

        // Set initial time
        UpdateTrayIcons();

        // Check if the app was launched by startup task
        bool launchedByStartup = IsLaunchedByStartupTask();

        // Only show and activate the window if not launched by startup
        if (!launchedByStartup)
        {
            //MainWindow.Activate();
        }


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
                SaveHoursOnlySetting(value);
                RaisePropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public bool IsStartupEnabled
    {
        get => _isStartupEnabled;
        set
        {
            if (_isStartupEnabled != value)
            {
                _isStartupEnabled = value;
                RaisePropertyChanged();
                UpdateStartup();
            }
        }
    }

    async void UpdateStartup()
    {
        var succeeded = await StartupManager.SetStartupEnabled(IsStartupEnabled);
        if (!succeeded)
        {
            // Disabled by policy
            // bugbug: show a message to the user, disable the toggle
            _isStartupEnabled = false;
            RaisePropertyChanged(nameof(IsStartupEnabled));
        }
    }

    private async Task LoadStartupSettingAsync()
    {
        _isStartupEnabled = await StartupManager.IsStartupEnabledAsync();
        RaisePropertyChanged(nameof(IsStartupEnabled));
    }

    private bool LoadHoursOnlySetting()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings.Values.ContainsKey(HoursOnlySettingKey))
        {
            return localSettings.Values[HoursOnlySettingKey] as bool? ?? false;
        }
        return false;
    }

    private void SaveHoursOnlySetting(bool hoursOnly)
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        localSettings.Values[HoursOnlySettingKey] = hoursOnly;
    }

    /// <summary>
    /// Checks if the application was launched by the startup task.
    /// </summary>
    /// <returns>True if launched by startup task, false otherwise.</returns>
    private bool IsLaunchedByStartupTask()
    {
        try
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedArgs != null)
            {
                // Check if the activation kind is StartupTask
                return activatedArgs.Kind == ExtendedActivationKind.StartupTask;
            }
        }
        catch (Exception)
        {
            // If we can't determine, default to false (show window)
        }

        return false;
    }

    private void Timer_Tick(object? sender, object e)
    {
        var now = DateTime.Now;
        if (now.Second != 0)
        {
            _timer.Stop();
            _timer.Interval = TimeSpan.FromSeconds(60 - now.Second);
            _timer.Start();
            _updateTimer = true;
        }
        else if (_updateTimer)
        {
            _updateTimer = false;
            _timer.Stop();
            _timer.Interval = TimeSpan.FromMinutes(1);
            _timer.Start();
        }

        UpdateTrayIcons();
        UpdateCurrentTimeDisplay();
    }

    private void UpdateTrayIcons()
    {
        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.Update();
        }
    }

    private void UpdateCurrentTimeDisplay()
    {
        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.UpdateCurrentTime();
        }
    }

    public void SaveTimeZones()
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

    public void LoadTimeZones()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        //var timeZoneList = new System.Collections.Generic.List<TimeZoneInfo>();

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
                        _timeNotifyIcons.Add(new TimeNotifyIcon(this, timeZone, cityName));

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
        _timeNotifyIcons.Add(new TimeNotifyIcon(this, timeZone, cityName));
        SaveTimeZones();
        UpdateTrayIcons();
    }

    public void RemoveTimeZone(TimeNotifyIcon timeNotifyIcon)
    {
        _timeNotifyIcons.Remove(timeNotifyIcon);
        timeNotifyIcon.Dispose();
        SaveTimeZones();
    }

    public ObservableCollection<TimeNotifyIcon> TimeNotifyIcons => _timeNotifyIcons;

    public void ExitApplication()
    {
        StopTimer();

        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.Dispose();
        }

        Environment.Exit(0);
    }

    public void StopTimer()
    {
        _timer?.Stop();
    }


}
