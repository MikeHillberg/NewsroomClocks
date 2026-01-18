using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

//using Microsoft.Windows.ApplicationModel.WindowsAppRuntime.Common;
using System;
using WinRT;

namespace NewsroomClocks
{
    internal class Program
    {
        static DispatcherQueue? _dispatcherQueue;

        [STAThread]
        static void Main(string[] args)
        { 
            // DeploymentManagerAutoInitializer is disabled,
            // because it breaks the unit tests,
            // so run it here manually.
            Microsoft.Windows.ApplicationModel.WindowsAppRuntime.DeploymentManagerCS.AutoInitialize.AccessWindowsAppSDK();

            // Register the provider that knows how to read from Assets folder
            App.AssetProvider = new AssetProvider();

            ComWrappersSupport.InitializeComWrappers();
            bool isRedirect = DecideRedirection();
            if (isRedirect)
            {
                return;
            }

            // Check if the app was launched by startup task
            bool launchedBySystemStartup = IsLaunchedBySystemStartup(args);

            // If launched automatically at startup, and there's a timezone to display,
            // run a dispatcher pump now before creating a Window orApp
            if (launchedBySystemStartup)
            {
                DispatcherQueueController controller = DispatcherQueueController.CreateOnCurrentThread();
                _dispatcherQueue = controller.DispatcherQueue;

                // Create the Manager singleton, which can be accessed by Manager.Instance
                Manager.EnsureCreated();

                // Run the initial event loop that just maintains the timer to update the systray
                // Skip this though if we have nothing to put into the systray
                if (Manager.Instance!.HasTimezones)
                {
                    _dispatcherQueue.RunEventLoop();
                }

                // When that dispatcher returns, it means we need to open the Window
                // So move from that Dispatcher to the one that Xaml creates in Application.Start
                controller.ShutdownQueue();
            }

            // Start Xaml, which will create/activate the MainWindow
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);

                Manager.EnsureCreated();
                new App();
            });
        }


        /// <summary>
        /// Checks if the application was launched automatically on boot
        /// </summary>
        static private bool IsLaunchedBySystemStartup(string[] commandLineArgs)
        {
            try
            {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                if (activatedArgs != null)
                {
                    if (activatedArgs.Kind == ExtendedActivationKind.StartupTask)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // If we can't determine, fall through to check command-line args
            }

            // Check for command-line argument
            if (commandLineArgs != null && commandLineArgs.Length > 0)
            {
                foreach (var arg in commandLineArgs)
                {
                    if (arg == "--systemStartup")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// This function terminates the initial dispatcher in order to let the Xaml App start
        /// </summary>
        static internal void StartApp()
        {
            _dispatcherQueue?.EnqueueEventLoopExit();
        }

        internal static ExtendedActivationKind ExtendedActivationKind;

        private static bool DecideRedirection()
        {
            bool isRedirect = false;

            // Find out what kind of activation this is
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            ExtendedActivationKind kind = args.Kind;
            ExtendedActivationKind = kind;

            try
            {
                // If this is a launch activation, or an automatic restart (after a reboot),
                // either register as the first instance or redirect to it
                if (kind == ExtendedActivationKind.Launch
                    || kind == ExtendedActivationKind.StartupTask)
                {
                    // Try to get the main instance (first instance)
                    var mainInstance = AppInstance.FindOrRegisterForKey("main");

                    // If this isn't the main instance, redirect to it
                    if (!mainInstance.IsCurrent)
                    {
                        isRedirect = true;
                        mainInstance.RedirectActivationToAsync(args).AsTask().Wait();
                    }
                    else
                    {
                        // This is the main instance, subscribe to activation events
                        // so that we can activate the window on redirection
                        mainInstance.Activated += OnActivated;
                    }
                }
            }
            catch (Exception)
            {
                // If redirection fails, continue with current instance
                isRedirect = false;
            }

            return isRedirect;
        }

        private static void OnActivated(object? sender, AppActivationArguments args)
        {
            // When another instance redirects to this main instance, show the window
            if (args.Kind == ExtendedActivationKind.Launch)
            {
                App.ShowMainWindow();
            }
        }
    }
}
