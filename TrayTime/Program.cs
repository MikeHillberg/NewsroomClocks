using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WinRT;

namespace TrayTime
{
    public class Program
    {
        static DispatcherQueue _dispatcherQueue;
        static Manager _manager;

        [STAThread]
        static void Main(string[] args)
        {
            ComWrappersSupport.InitializeComWrappers();
            bool isRedirect = DecideRedirection();
            if (isRedirect)
            {
                return;
            }


            // Create a DispatcherQueue and timer before Application.Start
            DispatcherQueueController controller = DispatcherQueueController.CreateOnCurrentThread();
            _dispatcherQueue = controller.DispatcherQueue;

            _manager = new Manager();

            //DispatcherQueueTimer timer = _dispatcherQueue.CreateTimer();
            //timer.Interval = TimeSpan.FromSeconds(5);
            //timer.Tick += (sender, e) =>
            //{
            //    timer.Stop();
            //    //Task.Run(() => controller.ShutdownQueue());
            //    dispatcherQueue.EnqueueEventLoopExit();
            //};
            //timer.Start();

            _dispatcherQueue.RunEventLoop();
            controller.ShutdownQueue();

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }

        static public void StartApp()
        {
            _dispatcherQueue.EnqueueEventLoopExit();
        }

        private static bool DecideRedirection()
        {
            bool isRedirect = false;

            // Find out what kind of activation this is
            Microsoft.Windows.AppLifecycle.AppActivationArguments args =
                Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            Microsoft.Windows.AppLifecycle.ExtendedActivationKind kind = args.Kind;

            try
            {
                // If this is a launch activation, check if we should redirect
                if (kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.Launch)
                {
                    // Try to get the main instance (first instance)
                    var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("main");

                    // If this isn't the main instance, redirect to it
                    if (!mainInstance.IsCurrent)
                    {
                        isRedirect = true;
                        mainInstance.RedirectActivationToAsync(args).AsTask().Wait();
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
    }
}
