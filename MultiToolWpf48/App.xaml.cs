using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool
{
    public partial class App : Application
    {
        private Mutex _singleInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Один экземпляр: горячие клавиши нельзя занять дважды.
            bool isFirst;
            _singleInstance = new Mutex(true, "MultiTool.SingleInstance", out isFirst);
            if (!isFirst)
            {
                MessageBox.Show("MultiTool уже запущен.", "MultiTool", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            DispatcherUnhandledException += OnUnhandledException;

            AppSettings.Current = SettingsStore.Load();
            AppServices.Start();

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppServices.Stop();
            if (_singleInstance != null) _singleInstance.Dispose();
            base.OnExit(e);
        }

        private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ActivityLog.Add("Ошибка", e.Exception.Message, LogKind.Error);
            e.Handled = true;
        }
    }
}
