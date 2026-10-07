using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            string text = await ClipboardHelper.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                FinishAccept(new AcceptOutcome(Services.AcceptStatus.Failed, "Буфер обмена пуст: скопируйте документ и повторите"));
                return;
            }

            FinishAccept(await AcceptWorkflow.RunAsync(text, AppSettings.Current, Window.GetWindow(this)));
        }

        private void FinishAccept(AcceptOutcome outcome)
        {
            bool failed = outcome.Status == Services.AcceptStatus.Failed;
            Show(AcceptResultText, !failed, outcome.Message);
            LogKind kind = outcome.Status == Services.AcceptStatus.Saved ? LogKind.Success
                         : outcome.Status == Services.AcceptStatus.Cancelled ? LogKind.Info
                         : LogKind.Error;
            ActivityLog.Add("Accept", outcome.Message, kind);
        }

        private void Show(TextBlock target, bool success, string message)
        {
            target.Text = message;
            target.Foreground = (Brush)FindResource(success ? "SuccessBrush" : "DangerBrush");
        }
    }
}
