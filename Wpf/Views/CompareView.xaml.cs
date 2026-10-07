using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using MultiTool.Services;

namespace MultiTool.Views;

public partial class CompareView : UserControl
{
    // Небольшая задержка, чтобы при вставке больших текстов сравнение не запускалось на каждый символ.
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public CompareView()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunCompare(); };
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) RunCompare();
    }

    private void OnCompare(object sender, RoutedEventArgs e) => RunCompare();

    private void OnSwap(object sender, RoutedEventArgs e)
    {
        (LeftBox.Text, RightBox.Text) = (RightBox.Text, LeftBox.Text);
        RunCompare();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        LeftBox.Clear();
        RightBox.Clear();
        RunCompare();
        LeftBox.Focus();
    }

    private void RunCompare()
    {
        _debounce.Stop();
        CompareResult r = TextComparer.Compare(LeftBox.Text, RightBox.Text, IgnoreCase.IsChecked == true);

        Fill(LeftResult, r.Left, r.LeftStates);
        Fill(RightResult, r.Right, r.RightStates);
        DetailsText.Text = r.IsEqual || r.IsEmpty ? "" : r.Details;

        Brush text, background, border;
        if (r.IsEmpty)
        {
            SummaryText.Text = "Вставьте тексты слева и справа — сравнение выполняется автоматически";
            text = (Brush)FindResource("MutedBrush");
            background = (Brush)FindResource("CardBrush");
            border = (Brush)FindResource("BorderBrush");
        }
        else if (r.IsEqual)
        {
            SummaryText.Text = $"Тексты совпадают полностью ({r.MaxLength} симв.)";
            text = (Brush)FindResource("SuccessBrush");
            background = (Brush)FindResource("SuccessSoftBrush");
            border = (Brush)FindResource("SuccessBrush");
        }
        else
        {
            SummaryText.Text = $"Совпало {r.Matches} из {r.MaxLength} симв.; отличается: {r.Mismatches}; " +
                               $"длина слева {r.Left.Length}, справа {r.Right.Length}";
            text = (Brush)FindResource("DangerBrush");
            background = (Brush)FindResource("DangerSoftBrush");
            border = (Brush)FindResource("DangerBrush");
        }

        SummaryText.Foreground = text;
        SummaryBorder.Background = background;
        SummaryBorder.BorderBrush = border;
    }

    // Подряд идущие символы одного состояния красятся одним куском — так быстрее.
    private void Fill(TextBlock target, string text, CharState[] states)
    {
        target.Inlines.Clear();
        int i = 0;
        while (i < text.Length)
        {
            int j = i + 1;
            while (j < text.Length && states[j] == states[i]) j++;

            target.Inlines.Add(new Run(text[i..j]) { Background = BrushFor(states[i]) });
            i = j;
        }
    }

    private Brush BrushFor(CharState state) => (Brush)FindResource(state switch
    {
        CharState.Match => "MatchBgBrush",
        CharState.Mismatch => "MismatchBgBrush",
        _ => "ExtraBgBrush"
    });
}
