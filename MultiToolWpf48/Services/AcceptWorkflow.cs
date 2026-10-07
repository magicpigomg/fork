using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using MultiTool.Models;

namespace MultiTool.Services
{
    public enum AcceptStatus { Saved, Cancelled, Failed }

    public sealed class AcceptOutcome
    {
        public AcceptOutcome(AcceptStatus status, string message, string titleLine = "", string savedPath = "")
        {
            Status = status;
            Message = message;
            TitleLine = titleLine;
            SavedPath = savedPath;
        }

        public AcceptStatus Status { get; private set; }
        public string Message { get; private set; }
        public string TitleLine { get; private set; }
        public string SavedPath { get; private set; }
    }

    /// <summary>Полный сценарий Accept: разбор документа → строка «ОТЗЫВ …» в буфер → макрос → сохранение файла.
    /// Используется и кнопкой на странице «Файлы», и горячей клавишей F1.</summary>
    public static class AcceptWorkflow
    {
        public static async Task<AcceptOutcome> RunAsync(string text, AppSettings s, Window owner)
        {
            AcceptFields fields;
            string error;
            if (!AcceptService.TryParse(text, s, out fields, out error))
                return Fail("Документ пустой либо неверные значения: " + error);

            // Как и раньше, в буфер сразу кладётся строка «ОТЗЫВ …», даже если дальше что-то пойдёт не так.
            if (!await ClipboardHelper.SetTextAsync(fields.TitleLine))
                return Fail("Буфер обмена занят другой программой");

            string macro;
            if (!AcceptService.TryBuildMacro(fields, s, out macro, out error))
                return Fail("Не удаётся преобразовать дату: " + error, fields.TitleLine);

            string path;
            if (s.AcceptAskPath)
            {
                path = AskPath(s, owner);
                if (path == null)
                    return new AcceptOutcome(AcceptStatus.Cancelled, "Сохранение отменено. В буфере: " + fields.TitleLine, fields.TitleLine);
            }
            else
            {
                path = s.AcceptSavePath;
                if (string.IsNullOrWhiteSpace(path))
                    return Fail("Не задан путь сохранения Accept: укажите его в настройках или включите запрос пути", fields.TitleLine);
            }

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, macro, FileGenerator.GetEncoding(s));
            }
            catch (Exception ex)
            {
                return Fail("Не удалось сохранить файл: " + ex.Message, fields.TitleLine);
            }

            return new AcceptOutcome(AcceptStatus.Saved, "Сохранено: " + path + ". В буфере: " + fields.TitleLine, fields.TitleLine, path);
        }

        private static AcceptOutcome Fail(string message, string titleLine = "")
        {
            return new AcceptOutcome(AcceptStatus.Failed, message, titleLine);
        }

        /// <summary>Окно «Сохранить как». Вызванное горячей клавишей, оно должно появиться поверх чужого окна —
        /// для этого владельцем служит невидимое окно поверх всех.</summary>
        private static string AskPath(AppSettings s, Window owner)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.mac)|*.mac",
                DefaultExt = ".mac",
                FileName = "!accept",
                Title = "Сохранить файл !accept.mac",
                OverwritePrompt = false
            };
            if (Directory.Exists(s.AcceptFolder)) dialog.InitialDirectory = s.AcceptFolder;

            Window helper = null;
            try
            {
                if (owner == null || !owner.IsVisible)
                {
                    helper = new Window
                    {
                        Width = 1,
                        Height = 1,
                        Left = 0,
                        Top = 0,
                        WindowStyle = WindowStyle.None,
                        AllowsTransparency = true,
                        Opacity = 0,
                        ShowInTaskbar = false,
                        Topmost = true
                    };
                    helper.Show();
                    owner = helper;
                }
                return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
            }
            finally
            {
                if (helper != null) helper.Close();
            }
        }
    }
}
