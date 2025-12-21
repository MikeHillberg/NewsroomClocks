using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace TrayTime;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

#if DEBUG
        // Add the control for optimizing CityMaps and windowsZones data files
        _root.Children.Add(
            new OptimizeDataFiles() 
            { 
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 30, 0, 0)
            });
#endif

        // When the user closes the window, hide it instead
        var appWindow = this.AppWindow;
        appWindow.Closing += (sender, args) =>
        {
            // If there aren't any time zones, then really quit;
            // otherwise there would be no way to exit the app
            if (!Manager.Instance!.HasTimezones)
            {
                return;
            }

            // Cancel the close; keep the app alive.
            args.Cancel = true;

            // Hide the window so it's no longer visible.
            sender.Hide();
        };
    }

    // Bind helper
    bool Not(bool b) => !b;

    /// <summary>
    /// Add a new time zone to the list
    /// </summary>
    async private void AddTimeZoneClick2(object sender, RoutedEventArgs e)
    {
        var dialog = new AddTimeZoneDialog()
        {
            XamlRoot = this.Content.XamlRoot
        };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            Manager.Instance!.AddTimeZone(
                dialog.CityInfo!.TimeZoneInfo!, 
                dialog.CityInfo.ToString());
        }
    }

    /// <summary>
    /// Remove a time zone from the lst
    /// </summary>
    private void DeleteTimeZone(object sender, RoutedEventArgs e)
    {
        TimeNotifyIcon icon = ((sender as Button)!.Tag as TimeNotifyIcon)!;
        Manager.Instance!.RemoveTimeZone(icon);
    }
}
