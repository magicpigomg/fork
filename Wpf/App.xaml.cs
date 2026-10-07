using System.Text;
using System.Windows.Threading;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Один экземпляр: горячие клавиши нельзя занять дважды.
        _singleInstance = new Mutex(true, "MultiTool.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("MultiTool уже запущен.", "MultiTool", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Нужно для записи файлов в ANSI-кодировке (Windows-1251) — как делал Encoding.Default в .NET Framework.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ActivityLog.Add("Ошибка", e.Exception.Message, LogKind.Error);
        e.Handled = true;
    }
}
