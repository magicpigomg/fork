using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MultiTool.Services
{
    /// <summary>Работа с буфером обмена с повторами: другой процесс (менеджер буфера, RDP, эмулятор)
    /// может держать его занятым.</summary>
    public static class ClipboardHelper
    {
        private const int Attempts = 15;
        private const int RetryDelayMs = 40;

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
                    // Данные могли попасть в буфер, а не удалась только финальная «фиксация» —
                    // тогда нужный текст уже лежит в буфере, и повторять запись не нужно.
                    if (ContainsExactly(text)) return true;
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
                    if (ContainsExactly(text)) return true;
                    Thread.Sleep(RetryDelayMs);
                }
            }
            return false;
        }

        private static bool ContainsExactly(string text)
        {
            try
            {
                return Clipboard.ContainsText() && Clipboard.GetText() == text;
            }
            catch (COMException)
            {
                return false;
            }
        }
    }
}
