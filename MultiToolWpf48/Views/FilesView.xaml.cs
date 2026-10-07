using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool.Views
{
    public partial class FilesView : UserControl
    {
        public FilesView()
        {
            InitializeComponent();
            Loaded += delegate { Refresh(); };
        }

        private void Refresh()
        {
            AppSettings s = AppSettings.Current;
            DateValue.Text = FileGenerator.CurrentDate(s);
            DatePathText.Text = string.IsNullOrWhiteSpace(s.DatePath) ? "не задан" : s.DatePath;
            ScriptPathText.Text = string.IsNullOrWhiteSpace(s.PriostanovleniePath) ? "не задан" : s.PriostanovleniePath;
        }

        // ── Приостановление ──────────────────────────────────────────────────────────────────

        private void OnCreateFiles(object sender, RoutedEventArgs e)
        {
            FileGenerator.Result result = FileGenerator.CreateAll(AppSettings.Current);
            Show(FilesStatus, result.Success, result.Message);
            ActivityLog.Add("Файлы", result.Message, result.Success ? LogKind.Success : LogKind.Error);
            Refresh();
        }

        // ── Accept ───────────────────────────────────────────────────────────────────────────

        private async void OnCreateAccept(object sender, RoutedEventArgs e)
        {
            AppSettings s = AppSettings.Current;

            string text = await ClipboardHelper.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                FailAccept("Буфер обмена пуст: скопируйте документ и повторите");
                return;
            }

            AcceptFields fields;
            string error;
            if (!AcceptService.TryParse(text, s, out fields, out error))
            {
                FailAccept("Документ пустой либо неверные значения: " + error);
                return;
            }

            // Как и раньше, в буфер сразу кладётся строка «ОТЗЫВ …», даже если дальше что-то пойдёт не так.
            if (!await ClipboardHelper.SetTextAsync(fields.TitleLine))
            {
                FailAccept("Буфер обмена занят другой программой");
                return;
            }

            string macro;
            if (!AcceptService.TryBuildMacro(fields, s, out macro, out error))
            {
                FailAccept("Не удаётся преобразовать дату: " + error);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.mac)|*.mac",
                DefaultExt = ".mac",
                FileName = "!accept",
                Title = "Сохранить файл !accept.mac",
                OverwritePrompt = false
            };
            if (Directory.Exists(s.AcceptFolder)) dialog.InitialDirectory = s.AcceptFolder;

            if (dialog.ShowDialog() != true)
            {
                Show(AcceptStatus, true, "Сохранение отменено. В буфере: " + fields.TitleLine);
                return;
            }

            try
            {
                File.WriteAllText(dialog.FileName, macro, FileGenerator.GetEncoding(s));
            }
            catch (Exception ex)
            {
                FailAccept("Не удалось сохранить файл: " + ex.Message);
                return;
            }

            string message = "Сохранено: " + dialog.FileName + ". В буфере: " + fields.TitleLine;
            Show(AcceptStatus, true, message);
            ActivityLog.Add("Accept", message, LogKind.Success);
        }

        private void FailAccept(string message)
        {
            Show(AcceptStatus, false, message);
            ActivityLog.Add("Accept", message, LogKind.Error);
        }

        private void Show(TextBlock target, bool success, string message)
        {
            target.Text = message;
            target.Foreground = (Brush)FindResource(success ? "SuccessBrush" : "DangerBrush");
        }
    }
}
