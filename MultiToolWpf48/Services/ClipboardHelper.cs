using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MultiTool.Services
{
    /// <summary>Работа с буфером обмена с повторами: другой процесс может держать его занятым.</summary>
    public static class ClipboardHelper
    {
        private const int Attempts = 10;
        private const int RetryDelayMs = 30;

        public static async Task<string> GetTextAsync()
        {
            for (int i = 0; i < Attempts; i++)
            {
                try
                {
                    return Clipboard.ContainsText() ? Clipboard.GetText() : null;
                }
                catch (COMException)
                {
                    await Task.Delay(RetryDelayMs);
                }
            }
            return null;
        }

        public static async Task<bool> SetTextAsync(string text)
        {
            for (int i = 0; i < Attempts; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return true;
                }
                catch (COMException)
                {
                    await Task.Delay(RetryDelayMs);
                }
            }
            return false;
        }

        /// <summary>Синхронная версия для обработчиков кнопок.</summary>
        public static bool TrySetText(string text)
        {
            for (int i = 0; i < Attempts; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return true;
                }
                catch (COMException)
                {
                    Thread.Sleep(RetryDelayMs);
                }
            }
            return false;
        }
    }
}
