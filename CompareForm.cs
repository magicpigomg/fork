using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// Добавьте этот файл в проект (Проект -> Добавить существующий элемент).
// Форма создаётся целиком кодом, дизайнер не нужен.
// Открытие из Form1:  new CompareForm().Show();
namespace WindowsFormsApp1
{
    public sealed class CompareForm : Form
    {
        private enum CharState { Match, Mismatch, Extra }

        private static readonly Color MatchColor = Color.FromArgb(198, 239, 206);    // зелёный
        private static readonly Color MismatchColor = Color.FromArgb(255, 160, 160); // красный
        private static readonly Color ExtraColor = Color.FromArgb(255, 235, 156);    // жёлтый: нет пары в другом тексте

        private const int MaxDetailLines = 200;

        private readonly RichTextBox _left = CreateBox();
        private readonly RichTextBox _right = CreateBox();
        private readonly Button _compare = new Button { Text = "Сравнить", AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
        private readonly Button _clear = new Button { Text = "Очистить", AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
        private readonly CheckBox _ignoreCase = new CheckBox { Text = "Не учитывать регистр", AutoSize = true, Margin = new Padding(15, 6, 3, 3) };
        private readonly Label _summary = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        private readonly TextBox _details = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5f)
        };

        public CompareForm()
        {
            Text = "Сравнение текстов";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 650);
            MinimumSize = new Size(600, 450);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            layout.Controls.Add(new Label { Text = "Текст слева:", AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label { Text = "Текст справа:", AutoSize = true }, 1, 0);
            layout.Controls.Add(_left, 0, 1);
            layout.Controls.Add(_right, 1, 1);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            buttons.Controls.Add(_compare);
            buttons.Controls.Add(_clear);
            buttons.Controls.Add(_ignoreCase);
            layout.Controls.Add(buttons, 0, 2);
            layout.SetColumnSpan(buttons, 2);

            layout.Controls.Add(_summary, 0, 3);
            layout.SetColumnSpan(_summary, 2);
            layout.Controls.Add(_details, 0, 4);
            layout.SetColumnSpan(_details, 2);

            Controls.Add(layout);

            _compare.Click += delegate { RunCompare(); };
            _clear.Click += delegate { ClearAll(); };
            _ignoreCase.CheckedChanged += delegate { RunCompare(); };
            AcceptButton = null;
        }

        private static RichTextBox CreateBox()
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11f),
                DetectUrls = false,
                HideSelection = false,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
        }

        private void ClearAll()
        {
            _left.Clear();
            _right.Clear();
            _details.Clear();
            _summary.Text = "";
            _left.Focus();
        }

        private void RunCompare()
        {
            // Заново присваиваем Text: убирает форматирование, которое могло прийти при вставке.
            string left = _left.Text;
            string right = _right.Text;
            _left.Text = left;
            _right.Text = right;

            int common = Math.Min(left.Length, right.Length);
            int max = Math.Max(left.Length, right.Length);
            var leftStates = new CharState[left.Length];
            var rightStates = new CharState[right.Length];
            var details = new StringBuilder();
            int matches = 0;
            int mismatches = 0;

            StringComparison cmp = _ignoreCase.Checked ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            for (int i = 0; i < common; i++)
            {
                if (string.Compare(left, i, right, i, 1, cmp) == 0)
                {
                    matches++;
                    leftStates[i] = CharState.Match;
                    rightStates[i] = CharState.Match;
                    continue;
                }

                mismatches++;
                leftStates[i] = CharState.Mismatch;
                rightStates[i] = CharState.Mismatch;
                if (mismatches <= MaxDetailLines)
                    details.AppendLine("Позиция " + (i + 1) + ": слева " + Describe(left[i]) + ", справа " + Describe(right[i]));
            }
            if (mismatches > MaxDetailLines)
                details.AppendLine("... и ещё " + (mismatches - MaxDetailLines) + " отличающихся символов");

            for (int i = common; i < left.Length; i++) leftStates[i] = CharState.Extra;
            for (int i = common; i < right.Length; i++) rightStates[i] = CharState.Extra;

            if (left.Length > common)
                details.AppendLine("Лишнее слева, с позиции " + (common + 1) + ": " + Quote(left.Substring(common)));
            if (right.Length > common)
                details.AppendLine("Лишнее справа, с позиции " + (common + 1) + ": " + Quote(right.Substring(common)));

            Highlight(_left, leftStates);
            Highlight(_right, rightStates);

            if (max == 0)
            {
                _summary.ForeColor = SystemColors.ControlText;
                _summary.Text = "Оба поля пустые";
            }
            else if (mismatches == 0 && left.Length == right.Length)
            {
                _summary.ForeColor = Color.DarkGreen;
                _summary.Text = "Тексты совпадают полностью (" + max + " симв.)";
            }
            else
            {
                _summary.ForeColor = Color.Firebrick;
                _summary.Text = "Совпало " + matches + " из " + max + " симв.; отличается: " + mismatches +
                                "; длина слева " + left.Length + ", справа " + right.Length;
            }

            _details.Text = details.ToString();
        }

        // Красит подряд идущие символы одного состояния одним куском — так быстрее.
        private static void Highlight(RichTextBox box, CharState[] states)
        {
            if (states.Length == 0) return;

            int selStart = box.SelectionStart;
            int selLength = box.SelectionLength;
            box.SuspendLayout();

            int runStart = 0;
            for (int i = 1; i <= states.Length; i++)
            {
                if (i < states.Length && states[i] == states[runStart]) continue;

                box.Select(runStart, i - runStart);
                box.SelectionBackColor = ColorOf(states[runStart]);
                runStart = i;
            }

            box.Select(selStart, selLength);
            box.ResumeLayout();
        }

        private static Color ColorOf(CharState state)
        {
            switch (state)
            {
                case CharState.Match: return MatchColor;
                case CharState.Mismatch: return MismatchColor;
                default: return ExtraColor;
            }
        }

        private static string Describe(char c)
        {
            switch (c)
            {
                case ' ': return "[пробел]";
                case '\t': return "[табуляция]";
                case '\n': return "[перевод строки]";
                case '\r': return "[возврат каретки]";
                case ' ': return "[неразрывный пробел]";
                default: return "'" + c + "'";
            }
        }

        private static string Quote(string s)
        {
            const int max = 60;
            string visible = s.Replace("\r", "").Replace("\n", "⏎").Replace("\t", "→");
            if (visible.Length > max) visible = visible.Substring(0, max) + "…";
            return "«" + visible + "»";
        }
    }
}
