using System.Windows;
using System.Windows.Controls;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool.Views
{
    public partial class HotkeysView : UserControl
    {
        private sealed class HotkeyCard
        {
            public HotkeyCard(string key, string title, string description)
            {
                Key = key;
                Title = title;
                Description = description;
            }

            public string Key { get; private set; }
            public string Title { get; private set; }
            public string Description { get; private set; }
        }

        private bool _syncing;

        public HotkeysView()
        {
            InitializeComponent();

            Log.ItemsSource = ActivityLog.Entries;
            ActivityLog.Entries.CollectionChanged += delegate { UpdateEmptyState(); };
            ActivityLog.LastValueChanged += UpdateLastValue;
            AppServices.HotkeysStateChanged += UpdateState;

            // Подписи Shift-клавиш берутся из настроек, поэтому обновляем при каждом показе.
            Loaded += delegate { BuildCards(); UpdateState(); UpdateLastValue(); UpdateEmptyState(); };
        }

        private void BuildCards()
        {
            AppSettings s = AppSettings.Current;
            Cards.ItemsSource = new[]
            {
                new HotkeyCard("F1", "Копирование и разбор",
                    "Ctrl+A → ПКМ → ↓ → → → Enter, затем по типу документа из буфера берётся нужный номер."),
                new HotkeyCard("F2", "Последовательность F2",
                    "F7 → Tab ×3 → Backspace → Tab ×8 → → → Tab ×3 → Enter."),
                new HotkeyCard("F3", "Последовательность F3",
                    "F7 → Tab ×4 → Ctrl+V → Tab ×8 → ← → Tab ×3 → Enter."),
                new HotkeyCard("Shift+F1", "Ввод текста", s.ShiftText1),
                new HotkeyCard("Shift+F2", "Ввод текста", s.ShiftText2),
                new HotkeyCard("Shift+F3", "Ввод текста", s.ShiftText3),
            };
        }

        private void UpdateState()
        {
            bool on = AppServices.HotkeysActive;
            _syncing = true;
            MasterSwitch.IsChecked = on;
            _syncing = false;
            StateText.Text = on
                ? "Включены: работают в любом окне, кроме заблокированных в настройках."
                : "Выключены: клавиши F1–F3 и Shift+F1–F3 ведут себя как обычно.";
        }

        private void UpdateLastValue()
        {
            string value = ActivityLog.LastValue;
            LastValueText.Text = string.IsNullOrEmpty(value) ? "—" : value;
            CopyLast.IsEnabled = !string.IsNullOrEmpty(value);
        }

        private void UpdateEmptyState()
        {
            EmptyLog.Visibility = ActivityLog.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnMasterChanged(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            AppServices.SetHotkeysEnabled(MasterSwitch.IsChecked == true);
        }

        private void OnCopyLast(object sender, RoutedEventArgs e)
        {
            if (ClipboardHelper.TrySetText(ActivityLog.LastValue))
                ActivityLog.Add("Буфер", "Значение скопировано", LogKind.Info);
        }

        private void OnClearLog(object sender, RoutedEventArgs e)
        {
            ActivityLog.Entries.Clear();
        }
    }
}
