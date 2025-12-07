using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Drawing = System.Drawing;

namespace TrayTime;

public class TimeNotifyIcon : IDisposable, INotifyPropertyChanged
{
    private Win32NotifyIcon _notifyIcon;
    private TimeZoneInfo _timeZoneInfo;
    private Manager _app;
    private string _cityName;

    static internal ReadOnlyCollection<TimeZoneInfo> AllTimeZones;

    public event PropertyChangedEventHandler? PropertyChanged;

    static TimeNotifyIcon()
    {
        AllTimeZones = TimeZoneInfo.GetSystemTimeZones();
    }

    internal string CityName => _cityName;

    internal TimeNotifyIcon(
        Manager app,
        TimeZoneInfo timeZoneInfo,
        string cityName)
    {
        _cityName = cityName;
        _timeZoneInfo = timeZoneInfo!;
        _app = app;

        _notifyIcon = new Win32NotifyIcon();
        _notifyIcon.Visible = true;
        _notifyIcon.Text = $"{_timeZoneInfo.StandardName}";

        _notifyIcon.MouseClick += NotifyIcon_MouseClick;

        CreateContextMenu();
    }

    internal TimeZoneInfo TimeZone
    {
        get => _timeZoneInfo;
        set
        {
            if (_timeZoneInfo != value)
            {
                _timeZoneInfo = value;
                RaisePropertyChanged();
                Update();
            }
        }
    }

    internal string CurrentTime
    {
        get
        {
            DateTime time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZoneInfo);
            return time.ToString("f");
        }
    }
    internal void UpdateCurrentTime()
    {
        RaisePropertyChanged(nameof(CurrentTime));
    }

    protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void NotifyIcon_MouseClick(object? sender, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            App.ShowMainWindow();
        }
    }

    void CreateContextMenu()
    {
        _notifyIcon.SetContextMenu(
            ("Exit", () => _app.ExitApplication())
        );
    }

    public void Update()
    {
        // Get current time in the specified time zone
        DateTime time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZoneInfo);

        bool hoursOnly = Manager.Instance!.HoursOnly;
        string timeText = hoursOnly ? time.ToString("hh") : time.ToString("h:mm");
        _notifyIcon.Text = $"{_timeZoneInfo.StandardName}: {time:h:mm tt}";

        // Dispose of the previous icon to avoid memory leak
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = GenerateIcon(timeText, time, hoursOnly);
        oldIcon?.Dispose();
    }

    private Drawing.Icon GenerateIcon(string timeText, DateTime time, bool hoursOnly)
    {
        // Create icon at actual display size for better text rendering
        int iconSize = 32;
        using var bitmap = new Drawing.Bitmap(iconSize, iconSize);
        using var graphics = Drawing.Graphics.FromImage(bitmap);

        // Determine colors based on time of day
        int currentHour = time.Hour;
        bool isDaytime = currentHour >= 6 && currentHour < 18;

        Drawing.Color backgroundColor = isDaytime ? Drawing.Color.Yellow : Drawing.Color.Black;
        Drawing.Color textColor = isDaytime ? Drawing.Color.Blue : Drawing.Color.White;

        // Fill background
        graphics.Clear(backgroundColor);

        if (hoursOnly)
        {
            // Display only the hour, centered
            using var font = new Drawing.Font("Segoe UI", 18, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var textBrush = new Drawing.SolidBrush(textColor);

            var stringFormat = new Drawing.StringFormat
            {
                Alignment = Drawing.StringAlignment.Center,
                LineAlignment = Drawing.StringAlignment.Center
            };

            // Draw hour centered in the icon
            graphics.DrawString(timeText, font, textBrush, new Drawing.RectangleF(0, 0, iconSize, iconSize), stringFormat);
        }
        else
        {
            // Split time into hour and minutes
            var timeParts = timeText.Split(':');
            string hour = timeParts.Length > 0 ? timeParts[0] : "";
            string minutes = timeParts.Length > 1 ? timeParts[1] : "";

            // Use a font size that fits two lines
            using var font = new Drawing.Font("Segoe UI", 18, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var textBrush = new Drawing.SolidBrush(textColor);

            var stringFormat = new Drawing.StringFormat
            {
                Alignment = Drawing.StringAlignment.Center,
                LineAlignment = Drawing.StringAlignment.Center
            };

            // Draw hour on top half
            graphics.DrawString(hour, font, textBrush, new Drawing.RectangleF(0, 1, iconSize, iconSize / 2), stringFormat);

            // Draw minutes on bottom half
            graphics.DrawString(minutes, font, textBrush, new Drawing.RectangleF(0, iconSize / 2 + 1, iconSize, (iconSize / 2) - 1), stringFormat);
        }

        // Convert bitmap to icon
        IntPtr hIcon = bitmap.GetHicon();
        var icon = Drawing.Icon.FromHandle(hIcon);

        return icon;
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
