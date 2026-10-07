using System.Windows.Media;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool.Views;

public partial class ToolsView : UserControl
{
    public ToolsView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshLabels();
    }

    // Подписи зависят от чисел в настройках.
    private void RefreshLabels()
    {
        var s = AppSettings.Current;
        NumberHint.Text = $"Пробелы убираются, затем: результат 1 — без первых {s.NumberSkipFirst} символов, " +
                          $"оставить {s.NumberTake}; результат 2 — без первых {s.NumberSkipSecond}; " +
                          $"результат 3 — первые {s.NumberTakeThird} символов результата 2.";
        Label1.Text = $"Результат 1 · {s.NumberTake} симв.";
        Label2.Text = $"Результат 2 · без первых {s.NumberSkipSecond}";
        Label3.Text = $"Результат 3 · первые {s.NumberTakeThird}";
    }

    // ── Очистка пробелов ─────────────────────────────────────────────────────────────────

    private void OnCleanSpaces(object sender, RoutedEventArgs e)
    {
        SpacesOutput.Text = TextTools.RemoveSpaces(SpacesInput.Text);
        Copy(SpacesOutput.Text, SpacesStatus);
    }

    private void OnCopySpaces(object sender, RoutedEventArgs e) => Copy(SpacesOutput.Text, SpacesStatus);

    // ── Разбор номера ────────────────────────────────────────────────────────────────────

    private void OnParseNumber(object sender, RoutedEventArgs e)
    {
        var parts = TextTools.ParseNumber(NumberInput.Text, AppSettings.Current);
        Result1.Text = parts.First;
        Result2.Text = parts.Second;
        Result3.Text = parts.Third;

        if (string.IsNullOrWhiteSpace(NumberInput.Text))
            SetStatus(NumberStatus, "Введите номер", "MutedBrush");
        else if (!parts.Complete)
            SetStatus(NumberStatus, "Номер короче ожидаемого — показано то, что удалось получить", "WarnBrush");
        else
            SetStatus(NumberStatus, "Готово", "SuccessBrush");
    }

    private void OnCopyResult(object sender, RoutedEventArgs e)
    {
        string text = ((Button)sender).Tag switch
        {
            "1" => Result1.Text,
            "2" => Result2.Text,
            _ => Result3.Text
        };
        Copy(text, NumberStatus);
    }

    // ── Общее ───────────────────────────────────────────────────────────────────────────

    private void Copy(string text, TextBlock status)
    {
        if (string.IsNullOrEmpty(text))
            SetStatus(status, "Нечего копировать", "MutedBrush");
        else if (ClipboardHelper.TrySetText(text))
            SetStatus(status, "Скопировано в буфер обмена", "SuccessBrush");
        else
            SetStatus(status, "Буфер обмена занят другой программой", "DangerBrush");
    }

    private void SetStatus(TextBlock status, string text, string brushKey)
    {
        status.Text = text;
        status.Foreground = (Brush)FindResource(brushKey);
    }
}
