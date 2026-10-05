using System.Windows;
using System.Threading;

namespace DDTM_DSFixUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Use a unique name, e.g. your app name + a GUID
            const string mutexName = "DDTM_DSFixUI-3F2A9C1E-7274-4D8E-A1C5-9E027D4F2778";

            _mutex = new Mutex(true, mutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // Already running: exit this one
                Shutdown();
                return;
            }

            base.OnStartup(e);
            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }

}
