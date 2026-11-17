using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool.Views
{
    public partial class CreditView : UserControl
    {
        public CreditView()
        {
            InitializeComponent();
            Loaded += delegate { ShowRates(CreditRates.Load()); };
        }

        // ── Курсы ────────────────────────────────────────────────────────────────────────────

        private void ShowRates(CreditRateSet set)
        {
            NbrbUsd.Text = Format(set.NbrbRate("USD"));
            NbrbEur.Text = Format(set.NbrbRate("EUR"));
            NbrbRub.Text = Format(set.NbrbRate("RUB"));
            AlfaUsd.Text = AlfaText(set, 0);
            AlfaEur.Text = AlfaText(set, 1);
            AlfaRub.Text = AlfaText(set, 2);
            NbrbTime.Text = Stamp(set.NbrbUpdated);
            AlfaTime.Text = Stamp(set.AlfaUpdated);
        }

        private static string Format(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.####", CultureInfo.InvariantCulture) : "—";
        }

        private static string AlfaText(CreditRateSet set, int index)
        {
            string line = set.AlfaLine(index);
            return string.IsNullOrWhiteSpace(line) ? "—" : line.Trim();
        }

        private static string Stamp(DateTime? time)
        {
            return time.HasValue ? "обновлено " + time.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) : "нет данных";
        }

        private async void OnUpdateRates(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;
            Show(RatesStatus, null, "Загрузка курсов…");
            try
            {
                CreditRatesUpdate update = await CreditRates.UpdateAsync(AppSettings.Current);
                ShowRates(CreditRates.Load());

                string message = "НБ РБ: " + update.NbrbMessage + "; банк: " + update.AlfaMessage;
                Show(RatesStatus, update.Success, message);
                ActivityLog.Add("Кредит", "Курсы — " + message, update.Success ? LogKind.Success : LogKind.Error);
            }
            finally
            {
                UpdateButton.IsEnabled = true;
            }
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                Process.Start("explorer.exe", "\"" + SettingsStore.Folder + "\"");
            }
            catch (Exception ex)
            {
                Show(RatesStatus, false, "Не удалось открыть папку: " + ex.Message);
            }
        }

        // ── Макросы ──────────────────────────────────────────────────────────────────────────

        private async void OnWithRate(object sender, RoutedEventArgs e) { await CreateAsync(CreditKind.WithRate, "с курсом"); }

        private async void OnNoRate(object sender, RoutedEventArgs e) { await CreateAsync(CreditKind.NoRate, "без курса"); }

        private async void OnConversion(object sender, RoutedEventArgs e) { await CreateAsync(CreditKind.Conversion, "конвертация"); }

        private async Task CreateAsync(CreditKind kind, string title)
        {
            string text = await ClipboardHelper.GetTextAsync();
            CreditBuildResult result = CreditMacro.Build(kind, text, CreditRates.Load());
            if (!result.Success)
            {
                Show(CreditStatus, false, result.Error);
                ActivityLog.Add("Кредит", "Кредит (" + title + "): " + result.Error, LogKind.Error);
                return;
            }

            var saved = new List<string>();
            try
            {
                foreach (CreditMacroFile file in result.Files)
                {
                    string path = AskPath(file);
                    if (path == null) continue;
                    File.WriteAllText(path, file.Text, FileGenerator.GetEncoding(AppSettings.Current));
                    saved.Add(file.FileName);
                }
            }
            catch (Exception ex)
            {
                Show(CreditStatus, false, "Не удалось сохранить файл: " + ex.Message);
                ActivityLog.Add("Кредит", "Кредит (" + title + "): не удалось сохранить файл: " + ex.Message, LogKind.Error);
                return;
            }

            if (saved.Count == 0)
            {
                Show(CreditStatus, null, "Сохранение отменено");
                ActivityLog.Add("Кредит", "Кредит (" + title + "): сохранение отменено", LogKind.Info);
                return;
            }

            string message = "Сохранено: " + string.Join(", ", saved) + " — " + result.Summary;
            Show(CreditStatus, true, message);
            ActivityLog.Add("Кредит", "Кредит (" + title + "): " + message, LogKind.Success);
        }

        private string AskPath(CreditMacroFile file)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.mac)|*.mac",
                DefaultExt = ".mac",
                FileName = file.FileName,
                Title = "Сохранить файл " + file.FileName,
                OverwritePrompt = false
            };
            Window owner = Window.GetWindow(this);
            bool? ok = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            return ok == true ? dialog.FileName : null;
        }

        /// <summary>success: true — зелёный, false — красный, null — нейтральный.</summary>
        private void Show(TextBlock target, bool? success, string message)
        {
            target.Text = message;
            target.Foreground = (Brush)FindResource(success == null ? "MutedBrush" : (success.Value ? "SuccessBrush" : "DangerBrush"));
        }
    }
}
