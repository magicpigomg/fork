using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MultiTool.Models;

namespace MultiTool.Services
{
    /// <summary>Создание файлов приостановления (перенос Priostanovlenie.DateFileCreate / FilePriostCreate).</summary>
    public static class FileGenerator
    {
        public struct Result
        {
            public Result(bool success, string message)
            {
                Success = success;
                Message = message;
            }

            public bool Success { get; }
            public string Message { get; }
        }

        public static string CurrentDate(AppSettings s)
        {
            return DateTime.Now.ToString(s.DateFormat, CultureInfo.InvariantCulture);
        }

        public static Result CreateAll(AppSettings s)
        {
            try
            {
                string date = CurrentDate(s);
                Write(s.DatePath, s.DateFileTemplate, date, s);
                Write(s.PriostanovleniePath, s.PriostanovlenieTemplate, date, s);
                return new Result(true, "Файлы созданы, дата: " + date);
            }
            catch (Exception ex)
            {
                return new Result(false, ex.Message);
            }
        }

        private static void Write(string path, string template, string date, AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Не указан путь к файлу (Настройки → Файлы)");

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Переводы строк — CRLF, как в исходной программе; WriteLine добавлял ещё один в конце.
            string text = Regex.Replace(template.Replace("{Date}", date), @"\r\n|\r|\n", "\r\n") + "\r\n";
            File.WriteAllText(path, text, GetEncoding(s));
        }

        // Encoding.Default в .NET Framework — это системная ANSI-кодировка (1251 на русской Windows).
        public static Encoding GetEncoding(AppSettings s)
        {
            return s.UseAnsiEncoding ? Encoding.GetEncoding((int)NativeMethods.GetACP()) : new UTF8Encoding(false);
        }
    }
}
