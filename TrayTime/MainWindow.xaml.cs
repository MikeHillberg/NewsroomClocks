using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Forms = System.Windows.Forms;
using Windows.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TrayTime;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private DispatcherTimer _timer;
    private bool _updateTimer = false;
    private const string HoursOnlySettingKey = "HoursOnlySetting";
    private const string TimeZonesSettingKey = "SavedTimeZones";

    ObservableCollection<TimeNotifyIcon> _timeNotifyIcons = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public MainWindow()
    {
        _timeNotifyIcons = new ObservableCollection<TimeNotifyIcon>();

        InitializeComponent();

        // Load settings after InitializeComponent to ensure proper threading context
        _hoursOnly = LoadHoursOnlySetting();
        
        // Load saved time zones or add default local time zone
        LoadTimeZones();

        // Load startup setting asynchronously after initialization
        _ = LoadStartupSettingAsync();

        // Get the AppWindow for this XAML Window (WinAppSDK 1.4+)
        var appWindow = this.AppWindow;

        appWindow.Closing += (sender, args) =>
        {
            // Cancel the close; keep the app alive.
            args.Cancel = true;

            // Hide the window so it's no longer visible.
            sender.Hide();
        };


        // Initialize the timer to update the tray icon every minute
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(DateTime.Now.Second);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        // Set initial time
        UpdateTrayIcons();

        // Hide the window on startup (after window is fully initialized)
        this.Activated += MainWindow_Activated;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Unsubscribe so this only runs once
        this.Activated -= MainWindow_Activated;
        
        // Hide the window after it's been activated
        this.AppWindow.Hide();
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


    bool _hoursOnly = false;
    internal bool HoursOnly
    {
        get => _hoursOnly;
        set
        {
            _hoursOnly = value;
            SaveHoursOnlySetting(value);
            UpdateTrayIcons();
            RaisePropertyChanged();
        }
    }

    bool _isStartupEnabled = false;
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
        if(!succeeded)
        {
            // Disabled by policy
            // bugbug: show a message to the user, disable the toggle
            _isStartupEnabled = false; 
            RaisePropertyChanged(nameof(IsStartupEnabled));
        }
    }

    bool Not(bool b) => !b;

    private void SaveTimeZones()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        var timeZoneIds = _timeNotifyIcons.Select(icon => icon.TimeZone.Id).ToArray();

        // Save as comma-separated string
        localSettings.Values[TimeZonesSettingKey] = string.Join(",", timeZoneIds);
    }

    private void LoadTimeZones()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        var timeZoneList = new System.Collections.Generic.List<TimeZoneInfo>();

        if (localSettings.Values.ContainsKey(TimeZonesSettingKey))
        {
            var savedTimeZones = localSettings.Values[TimeZonesSettingKey] as string;
            if (!string.IsNullOrEmpty(savedTimeZones))
            {
                var timeZoneIds = savedTimeZones.Split(',', StringSplitOptions.RemoveEmptyEntries);

                foreach (var timeZoneId in timeZoneIds)
                {
                    try
                    {
                        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                        timeZoneList.Add(timeZone);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        // If time zone is not found, skip it
                        continue;
                    }
                }
            }
        }

        // If no saved time zones, add default local time zone
        if (timeZoneList.Count == 0)
        {
            timeZoneList.Add(TimeZoneInfo.Local);
        }

        // Create TimeNotifyIcon instances in reverse order
        for (int i = timeZoneList.Count - 1; i >= 0; i--)
        {
            _timeNotifyIcons.Add(new TimeNotifyIcon(this, timeZoneList[i]));
        }
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

    private void TimeZoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        TimeZoneInfo selectedTimeZoneInfo = ((sender as ComboBox)!.SelectedItem as TimeZoneInfo)!;
        TimeNotifyIcon selectedTimeNotifyIcon = ((sender as ComboBox)!.Tag as TimeNotifyIcon)!;
        if (selectedTimeNotifyIcon == null || selectedTimeZoneInfo == null)
        {
            return;
        }

        selectedTimeNotifyIcon.TimeZone = selectedTimeZoneInfo;
        SaveTimeZones();
    }

    private void UpdateCurrentTimeDisplay()
    {
        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.UpdateCurrentTime();
        }
    }

    internal void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            this.AppWindow.Show();
        }
    }

    private void UpdateTrayIcons()
    {
        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.Update();
        }
    }

    internal void ExitApplication()
    {
        _timer.Stop();

        foreach (var notifyIcon in _timeNotifyIcons)
        {
            notifyIcon.Dispose();
        }

        Environment.Exit(0);
    }

    private void ComboBox_Loaded(object sender, RoutedEventArgs e)
    {
    }

    private void AddTimeZoneClick(object sender, RoutedEventArgs e)
    {
        _timeNotifyIcons.Add(new TimeNotifyIcon(this, TimeZoneInfo.Local));
        SaveTimeZones();
    }

    private async void UpdateAutoStartup()
    {
        // bugbug: should disable UI during an async operation
        bool success = await StartupManager.SetStartupEnabled(IsStartupEnabled);
        {
            IsStartupEnabled = false;
        }
    }

    private async Task LoadStartupSettingAsync()
    {
        _isStartupEnabled = await StartupManager.IsStartupEnabledAsync();
        RaisePropertyChanged(nameof(IsStartupEnabled));
    }
}
