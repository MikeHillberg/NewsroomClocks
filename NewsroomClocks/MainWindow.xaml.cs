using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using Windows.System;

namespace NewsroomClocks;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Set the window icon
        var appWindow = this.AppWindow;
        appWindow.SetIcon("Assets\\HoursMinutesExample.ico");

        // When the user closes the window, hide it instead
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

        SetupDebug(_root, appWindow);
    }

    string DaytimeRange
    {
        get
        {
            var daytimeStart = new DateTime(1, 1, 1, 6, 0, 0);
            var daytimeEnd = new DateTime(1, 1, 1, 18, 0, 0);
            return $"{daytimeStart.ToString("t")} - {daytimeEnd.ToString("t")}";
        }
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

    /// <summary>
    /// Set up debug code to run file generators
    /// </summary>
    [Conditional("DEBUG")]
    static void SetupDebug(Panel root, Microsoft.UI.Windowing.AppWindow appWindow)
    {
        // Listen for Control+Shift+Alt+G to generate data files
        root.KeyUp += (sender, args) =>
        {
            if (args.Key == VirtualKey.G)
            {
                var keyboardSource = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
                var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
                var altState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);

                bool isControlDown = (keyboardSource & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                bool isShiftDown = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                bool isAltDown = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                if (isControlDown && isShiftDown && isAltDown)
                {
                    // Generate the data files
                    GenerateDataFiles.Generate();
                }
            }
        };
    }
}
