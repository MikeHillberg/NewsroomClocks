using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace TrayTime
{
    public partial class App : Application, INotifyPropertyChanged
    {
        static internal Window? MainWindow = null;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        static App? _instance;
        public static App Instance => _instance!;

        public static IAssetProvider? AssetProvider;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
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

        static public void ShowMainWindow()
        {
            if (MainWindow == null)
            {
                Program.StartApp();
            }
            else
            {
                MainWindow.Activate();
            }

        }

    }
}
