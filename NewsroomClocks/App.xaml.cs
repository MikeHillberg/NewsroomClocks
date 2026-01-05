using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Foundation;

namespace NewsroomClocks;

public partial class App : Application
{
    static internal Window? MainWindow = null;
    static App? _instance;

    internal static App Instance => _instance!;

    public static IAssetProvider? AssetProvider;

    public App()
    {
        _instance = this;
        InitializeComponent();
        
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        //MainWindow.Activate();
        BringMainWindowToForeground();
    }

    /// <summary>
    /// Show the MainWindow in the foreground, creating it if necessary.
    /// </summary>
    static internal void ShowMainWindow()
    {
        if (MainWindow == null)
        {
            // App and MainWindow haven't been created yet, start the app and it will show the window
            Program.StartApp();
        }
        else
        {
            // Unhide the window and bring to foreground,
            // moving to the UI thread if necessary (activation redirection case)
            if (DispatcherQueue.GetForCurrentThread() == null)
            {
                MainWindow.DispatcherQueue.TryEnqueue(BringMainWindowToForeground);
            }
            else
            {
                BringMainWindowToForeground();
            }
        }
    }

    /// <summary>
    /// Bring MainWindow to foregrond with Show, SetForegroundWindow, and Activate.
    /// (In some scenarios just MainWindow.Activate isn't enough)
    /// </summary>
    private static void BringMainWindowToForeground()
    {
        var mainWindowhwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(MainWindow!));
        
        // Restore if minimized
        PInvoke.ShowWindow(mainWindowhwnd, SHOW_WINDOW_CMD.SW_RESTORE);
        
        // Bring to foreground
        PInvoke.SetForegroundWindow(mainWindowhwnd);

        // Also call Activate as a fallback
        MainWindow!.Activate();
    }
}
