using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Windows.Storage;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace TrayTime
{
    public partial class App : Application, INotifyPropertyChanged
    {
        static internal Window? MainWindow;
        private const string HoursOnlySettingKey = "HoursOnlySetting";
        private const string TimeZonesSettingKey = "SavedTimeZones";
        private bool _hoursOnly = false;
        private DispatcherTimer _timer;
        private bool _updateTimer = false;
        private ObservableCollection<TimeNotifyIcon> _timeNotifyIcons = new();
        private bool _isStartupEnabled = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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

        static App? _instance;
        public static App Instance => _instance!;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            _instance = this;
            InitializeComponent();
            
            // Load settings
            _hoursOnly = LoadHoursOnlySetting();

            // Load startup setting asynchronously
            _ = LoadStartupSettingAsync();

            // Initialize the timer to update the tray icon every minute
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(DateTime.Now.Second);
            _timer.Tick += Timer_Tick;
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
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();

            // Check if the app was launched by startup task
            bool launchedByStartup = IsLaunchedByStartupTask();

            // Only show and activate the window if not launched by startup
            if (!launchedByStartup)
            {
                MainWindow.Activate();
            }

            // Load saved time zones
            LoadTimeZones();

            // Start timer after window is created
            _timer.Start();

            // Set initial time
            UpdateTrayIcons();
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
                            if(parts.Length != 2)
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

        public void ShowMainWindow()
        {
            MainWindow?.AppWindow.Show();
        }

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
}
