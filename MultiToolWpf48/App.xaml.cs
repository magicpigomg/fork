using System;
using System.IO;
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

            // Любые необработанные ошибки пишем в crash.log; ошибки запуска показываем в окне.
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                WriteCrashLog("Необработанное исключение", args.ExceptionObject as Exception);
            DispatcherUnhandledException += OnUnhandledException;

            try
            {
                // Один экземпляр: горячие клавиши нельзя занять дважды.
                bool isFirst;
                _singleInstance = new Mutex(true, "MultiTool.SingleInstance", out isFirst);
                if (!isFirst)
                {
                    MessageBox.Show("MultiTool уже запущен.", "MultiTool", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                AppSettings.Current = SettingsStore.Load();
                AppServices.Start();

                var window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                ReportFatal("Не удалось запустить приложение", ex);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppServices.Stop();
            if (_singleInstance != null) _singleInstance.Dispose();
            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            // Пока главное окно не показано, продолжать нечего — иначе процесс повиснет без окна.
            if (MainWindow == null || !MainWindow.IsVisible)
            {
                ReportFatal("Ошибка при запуске", e.Exception);
                return;
            }

            WriteCrashLog("Ошибка во время работы", e.Exception);
            ActivityLog.Add("Ошибка", e.Exception.Message, LogKind.Error);
        }

        /// <summary>Показывает полный текст ошибки (с внутренними исключениями), пишет лог и закрывает приложение.</summary>
        private void ReportFatal(string stage, Exception ex)
        {
            string log = WriteCrashLog(stage, ex);
            string details = ex == null ? "(нет данных)" : ex.ToString();
            if (details.Length > 1800) details = details.Substring(0, 1800) + "…";

            MessageBox.Show(
                stage + ":\n\n" + details + "\n\nПолный текст сохранён в файл:\n" + log,
                "MultiTool — ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }

        private static string WriteCrashLog(string stage, Exception ex)
        {
            string path = Path.Combine(SettingsStore.Folder, "crash.log");
            try
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + stage + Environment.NewLine +
                    (ex == null ? "(нет данных)" : ex.ToString()) + Environment.NewLine +
                    new string('-', 70) + Environment.NewLine);
            }
            catch
            {
                // лог не критичен
            }
            return path;
        }
    }
}
