using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using MultiTool.Services;

namespace MultiTool.Views
{
    public partial class CompareView : UserControl
    {
        private enum Verdict { Match, Mismatch, Warning, Neutral }

        // Небольшая задержка, чтобы при вставке больших текстов сравнение не запускалось на каждый символ.
        private readonly DispatcherTimer _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        // Значения платежа, выбранные кнопками (null — кнопка для этой стороны ещё не нажималась).
        private PaymentFields _siteFields;
        private PaymentFields _docFields;

        public CompareView()
        {
            InitializeComponent();
            _debounce.Tick += delegate { _debounce.Stop(); if (!FieldsMode) RunCompare(); };
        }

        private bool FieldsMode
        {
            get { return ModeFields.IsChecked == true; }
        }

        private void OnInputChanged(object sender, TextChangedEventArgs e)
        {
            _debounce.Stop();
            _debounce.Start();
        }

        private void OnOptionChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            if (FieldsMode) RefreshFields(); else RunCompare();
        }

        private void OnCompare(object sender, RoutedEventArgs e)
        {
            if (FieldsMode) RefreshFields(); else RunCompare();
        }

        private void OnSwap(object sender, RoutedEventArgs e)
        {
            string left = LeftBox.Text;
            LeftBox.Text = RightBox.Text;
            RightBox.Text = left;

            PaymentFields tmp = _siteFields;
            _siteFields = _docFields;
            _docFields = tmp;

            if (FieldsMode) RefreshFields(); else RunCompare();
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            LeftBox.Clear();
            RightBox.Clear();
            _siteFields = null;
            _docFields = null;
            if (FieldsMode) RefreshFields(); else RunCompare();
            LeftBox.Focus();
        }

        // ── Выбор значений платежа ───────────────────────────────────────────────────────────

        private void OnExtractSite(object sender, RoutedEventArgs e)
        {
            _siteFields = PaymentParser.ParseSite(LeftBox.Text);
            ShowFields();
        }

        private void OnExtractDoc(object sender, RoutedEventArgs e)
        {
            _docFields = PaymentParser.ParseDocument(RightBox.Text);
            ShowFields();
        }

        private void ShowFields()
        {
            if (!FieldsMode) ModeFields.IsChecked = true; // сработает OnModeChanged и обновит таблицу
            else RefreshFields();
        }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            bool fields = FieldsMode;
            FieldsPanel.Visibility = fields ? Visibility.Visible : Visibility.Collapsed;
            TextResultsPanel.Visibility = fields ? Visibility.Collapsed : Visibility.Visible;
            InputRow.Height = new GridLength(fields ? 120 : 170);
            if (fields) RefreshFields(); else RunCompare();
        }

        /// <summary>Строит таблицу «поле — слева — справа — итог» и сравнивает значения посимвольно.</summary>
        private void RefreshFields()
        {
            FieldsGrid.Children.Clear();
            FieldsGrid.RowDefinitions.Clear();
            FieldsGrid.ColumnDefinitions.Clear();
            FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
            FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddHeaderRow();

            bool ignoreCase = IgnoreCase.IsChecked == true;
            bool bothParsed = _siteFields != null && _docFields != null;
            string[] left = _siteFields != null ? _siteFields.ToArray() : null;
            string[] right = _docFields != null ? _docFields.ToArray() : null;

            int matched = 0, mismatched = 0, warnings = 0;
            for (int i = 0; i < PaymentFieldInfo.Names.Length; i++)
            {
                string lv = left != null ? left[i] : null;
                string rv = right != null ? right[i] : null;
                bool optional = PaymentFieldInfo.Optional[i];

                CompareResult cmp = null;
                Verdict verdict = Verdict.Neutral;
                string verdictText;

                if (!bothParsed)
                {
                    verdictText = "ждём вторую сторону";
                }
                else if (string.IsNullOrEmpty(lv) && string.IsNullOrEmpty(rv))
                {
                    if (optional) { verdict = Verdict.Neutral; verdictText = "нет в обоих — допустимо"; }
                    else { verdict = Verdict.Mismatch; verdictText = "не найдено нигде"; mismatched++; }
                }
                else if (string.IsNullOrEmpty(lv) || string.IsNullOrEmpty(rv))
                {
                    string side = string.IsNullOrEmpty(lv) ? "слева" : "справа";
                    if (optional) { verdict = Verdict.Warning; verdictText = "нет " + side; warnings++; }
                    else { verdict = Verdict.Mismatch; verdictText = "не найдено " + side; mismatched++; }
                }
                else
                {
                    cmp = TextComparer.Compare(lv, rv, ignoreCase);
                    if (cmp.IsEqual) { verdict = Verdict.Match; verdictText = "совпадает"; matched++; }
                    else
                    {
                        string detail = cmp.Mismatches > 0 ? cmp.Mismatches + " симв." : "";
                        if (lv.Length != rv.Length)
                            detail += (detail.Length > 0 ? ", " : "") + "длина " + lv.Length + " и " + rv.Length;
                        verdict = Verdict.Mismatch;
                        verdictText = "отличается: " + detail;
                        mismatched++;
                    }
                }

                AddFieldRow(i + 1, PaymentFieldInfo.Names[i], optional, lv, rv, cmp, verdict, verdictText);
            }

            UpdateFieldsSummary(bothParsed, matched, mismatched, warnings);
        }

        private void AddHeaderRow()
        {
            FieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] titles = { "Поле", "Слева (сайт)", "Справа (документ)", "Итог" };
            for (int c = 0; c < titles.Length; c++)
            {
                var text = new TextBlock
                {
                    Text = titles[c],
                    Style = (Style)FindResource("FieldLabel"),
                    Margin = new Thickness(6, 0, 6, 8)
                };
                Grid.SetRow(text, 0);
                Grid.SetColumn(text, c);
                FieldsGrid.Children.Add(text);
            }
        }

        private void AddFieldRow(int row, string name, bool optional, string left, string right,
            CompareResult cmp, Verdict verdict, string verdictText)
        {
            FieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // линия-разделитель под строкой
            var line = new Border
            {
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            Grid.SetRow(line, row);
            Grid.SetColumnSpan(line, 4);
            FieldsGrid.Children.Add(line);

            var label = new TextBlock
            {
                Text = name + (optional ? " (если есть)" : ""),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6, 8, 6, 8)
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            FieldsGrid.Children.Add(label);

            AddValueCell(row, 1, left, cmp != null ? cmp.LeftStates : null, cmp != null ? cmp.Left : null);
            AddValueCell(row, 2, right, cmp != null ? cmp.RightStates : null, cmp != null ? cmp.Right : null);

            var chip = new Border
            {
                Background = (Brush)FindResource(verdict == Verdict.Match ? "SuccessSoftBrush"
                    : verdict == Verdict.Mismatch ? "DangerSoftBrush"
                    : verdict == Verdict.Warning ? "AccentSoftBrush" : "InputBrush"),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(6, 5, 0, 5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = verdictText,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 170,
                    Foreground = (Brush)FindResource(verdict == Verdict.Match ? "SuccessBrush"
                        : verdict == Verdict.Mismatch ? "DangerBrush"
                        : verdict == Verdict.Warning ? "WarnBrush" : "MutedBrush")
                }
            };
            Grid.SetRow(chip, row);
            Grid.SetColumn(chip, 3);
            FieldsGrid.Children.Add(chip);
        }

        /// <summary>Значение показывается посимвольно: совпавшие символы зелёные, отличающиеся красные, лишние жёлтые.</summary>
        private void AddValueCell(int row, int column, string value, CharState[] states, string comparedText)
        {
            var block = new TextBlock
            {
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 5, 8, 5)
            };

            if (string.IsNullOrEmpty(value))
            {
                block.Text = "—";
                block.Foreground = (Brush)FindResource("MutedBrush");
            }
            else if (states != null)
            {
                Fill(block, comparedText, states);
            }
            else
            {
                block.Text = value; // вторая сторона ещё не выбрана — просто показываем значение
            }

            Grid.SetRow(block, row);
            Grid.SetColumn(block, column);
            FieldsGrid.Children.Add(block);
        }

        private void UpdateFieldsSummary(bool bothParsed, int matched, int mismatched, int warnings)
        {
            string text;
            Brush foreground, background, border;

            if (!bothParsed)
            {
                string waiting = _siteFields == null && _docFields == null
                    ? "Вставьте текст сайта слева и документа справа, затем нажмите обе кнопки «Выбрать значения…»"
                    : _siteFields == null
                        ? "Значения из документа выбраны. Нажмите «Выбрать значения из сайта (слева)»"
                        : "Значения из сайта выбраны. Нажмите «Выбрать значения из документа (справа)»";
                text = waiting + FoundHint();
                foreground = (Brush)FindResource("MutedBrush");
                background = (Brush)FindResource("CardBrush");
                border = (Brush)FindResource("BorderBrush");
            }
            else if (mismatched == 0 && warnings == 0)
            {
                text = "Все поля совпадают (" + matched + " из " + PaymentFieldInfo.Names.Length + ")" + FoundHint();
                foreground = (Brush)FindResource("SuccessBrush");
                background = (Brush)FindResource("SuccessSoftBrush");
                border = foreground;
            }
            else
            {
                text = "Совпало " + matched + " из " + PaymentFieldInfo.Names.Length + "; отличается: " + mismatched +
                       (warnings > 0 ? "; есть только с одной стороны: " + warnings : "") + FoundHint();
                bool bad = mismatched > 0;
                foreground = (Brush)FindResource(bad ? "DangerBrush" : "WarnBrush");
                background = (Brush)FindResource(bad ? "DangerSoftBrush" : "CardBrush");
                border = foreground;
            }

            SummaryText.Text = text;
            SummaryText.Foreground = foreground;
            SummaryBorder.Background = background;
            SummaryBorder.BorderBrush = border;
        }

        /// <summary>Подсказка, сколько полей найдено; если мало — вероятно, вставлен не тот текст.</summary>
        private string FoundHint()
        {
            string hint = "";
            int total = PaymentFieldInfo.Names.Length;
            if (_siteFields != null) hint += "  ·  слева найдено " + _siteFields.FoundCount + " из " + total;
            if (_docFields != null) hint += "  ·  справа найдено " + _docFields.FoundCount + " из " + total;
            return hint;
        }

        // ── Сравнение всего текста ───────────────────────────────────────────────────────────

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
                SummaryText.Text = "Тексты совпадают полностью (" + r.MaxLength + " симв.)";
                text = (Brush)FindResource("SuccessBrush");
                background = (Brush)FindResource("SuccessSoftBrush");
                border = (Brush)FindResource("SuccessBrush");
            }
            else
            {
                SummaryText.Text = "Совпало " + r.Matches + " из " + r.MaxLength + " симв.; отличается: " + r.Mismatches +
                                   "; длина слева " + r.Left.Length + ", справа " + r.Right.Length;
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

                target.Inlines.Add(new Run(text.Substring(i, j - i)) { Background = BrushFor(states[i]) });
                i = j;
            }
        }

        private Brush BrushFor(CharState state)
        {
            string key = state == CharState.Match ? "MatchBgBrush" : state == CharState.Mismatch ? "MismatchBgBrush" : "ExtraBgBrush";
            return (Brush)FindResource(key);
        }
    }
}
