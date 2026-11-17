using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool.Views
{
    public partial class CreditView : UserControl
    {
        private bool _askedFolder;

        public CreditView()
        {
            InitializeComponent();
            Loaded += delegate
            {
                ShowFolder();
                ShowRates(CreditRates.Load());

                // Первое открытие: папка для курсов ещё не выбрана — предлагаем выбрать (после отрисовки страницы).
                if (!_askedFolder && string.IsNullOrWhiteSpace(AppSettings.Current.CreditRatesFolder))
                {
                    _askedFolder = true;
                    Dispatcher.BeginInvoke(new Action(delegate { ChooseFolder(true); }), DispatcherPriority.ApplicationIdle);
                }
            };
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

        private void ShowFolder()
        {
            FolderText.Text = CreditRates.Folder;
        }

        private void OnChooseFolder(object sender, RoutedEventArgs e)
        {
            ChooseFolder(false);
        }

        /// <summary>Окно выбора папки для курсов. При первом открытии отказ оставляет папку настроек программы (и больше не спрашивает).</summary>
        private void ChooseFolder(bool first)
        {
            string current = CreditRates.Folder;

            // Подсказка видна и под окном выбора; окно — стандартное WPF-окно открытия файла: нужная папка открывается в нём
            // и подтверждается кнопкой «Открыть» (так не нужна сборка System.Windows.Forms).
            Show(RatesStatus, null, (first ? "Первое открытие вкладки «Кредит». " : "") +
                "Выберите папку для курсов валют: откройте её в окне и нажмите «Открыть». В ней будут лежать rates_nbrb.json и rates_alfa.txt; " +
                "если такие файлы там уже есть (например, от старой программы Credit), они подхватятся.");

            var dialog = new OpenFileDialog
            {
                Title = "Папка для курсов валют: откройте её и нажмите «Открыть»",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                AddExtension = false,
                DereferenceLinks = true,
                Filter = "Папки|*.без-файлов",
                FileName = "Выбор папки"
            };
            if (Directory.Exists(current)) dialog.InitialDirectory = current;

            Window owner = Window.GetWindow(this);
            bool? ok = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

            string picked = null;
            if (ok == true)
            {
                string name = dialog.FileName;
                string chosen = Directory.Exists(name) ? name : Path.GetDirectoryName(name);
                if (!string.IsNullOrWhiteSpace(chosen) && Directory.Exists(chosen)) picked = chosen;
            }

            if (picked == null)
            {
                if (!first) return;
                Apply(SettingsStore.Folder);
                Show(RatesStatus, null, "Папка не выбрана — курсы будут лежать в папке настроек. Сменить её можно кнопкой «Выбрать папку…».");
                return;
            }

            Apply(picked);
            CreditRateSet found = CreditRates.Load();
            bool hasRates = found.NbrbUpdated.HasValue || found.AlfaUpdated.HasValue;
            string message = hasRates
                ? "Папка выбрана, найдены сохранённые курсы."
                : "Папка выбрана. Курсов в ней пока нет — нажмите «Обновить курсы».";
            Show(RatesStatus, true, message);
            ActivityLog.Add("Кредит", "Папка курсов: " + picked, LogKind.Info);
        }

        private void Apply(string folder)
        {
            AppSettings.Current.CreditRatesFolder = folder;
            string error = SettingsStore.Save(AppSettings.Current);
            ShowFolder();
            ShowRates(CreditRates.Load());
            if (error != null) Show(RatesStatus, false, "Папка выбрана, но настройки не сохранились: " + error);
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
                string folder = CreditRates.Folder;
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", "\"" + folder + "\"");
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
