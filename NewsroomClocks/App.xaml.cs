using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

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
        MainWindow.Activate();
    }

    static internal void ShowMainWindow()
    {
        if (MainWindow == null)
        {
            // App and MainWindow haven't been created yet
            Program.StartApp();
        }
        else
        {
            // Unhide the window
            MainWindow.Activate();
        }
    }
}
