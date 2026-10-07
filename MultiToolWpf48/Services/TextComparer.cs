using System;
using System.Text;

namespace MultiTool.Services
{
    public enum CharState { Match, Mismatch, Extra }

    public sealed class CompareResult
    {
        public string Left { get; set; }
        public string Right { get; set; }
        public CharState[] LeftStates { get; set; }
        public CharState[] RightStates { get; set; }
        public int Matches { get; set; }
        public int Mismatches { get; set; }
        public string Details { get; set; }

        public int MaxLength { get { return Math.Max(Left.Length, Right.Length); } }
        public bool IsEmpty { get { return MaxLength == 0; } }
        public bool IsEqual { get { return Mismatches == 0 && Left.Length == Right.Length; } }
    }

    /// <summary>Посимвольное сравнение: первый символ с первым, второй со вторым и т.д.</summary>
    public static class TextComparer
    {
        private const int MaxDetailLines = 200;

        public static CompareResult Compare(string left, string right, bool ignoreCase)
        {
            // Переводы строк приводим к одному виду, чтобы \r\n и \n не считались отличием.
            left = left.Replace("\r\n", "\n");
            right = right.Replace("\r\n", "\n");

            int common = Math.Min(left.Length, right.Length);
            var leftStates = new CharState[left.Length];
            var rightStates = new CharState[right.Length];
            var details = new StringBuilder();
            int matches = 0, mismatches = 0;

            StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            for (int i = 0; i < common; i++)
            {
                if (string.Compare(left, i, right, i, 1, comparison) == 0)
                {
                    matches++;
                    continue; // Match = 0 — значение по умолчанию
                }

                mismatches++;
                leftStates[i] = rightStates[i] = CharState.Mismatch;
                if (mismatches <= MaxDetailLines)
                    details.AppendLine("Позиция " + (i + 1) + ": слева " + Describe(left[i]) + ", справа " + Describe(right[i]));
            }
            if (mismatches > MaxDetailLines)
                details.AppendLine("… и ещё " + (mismatches - MaxDetailLines) + " отличающихся символов");

            for (int i = common; i < left.Length; i++) leftStates[i] = CharState.Extra;
            for (int i = common; i < right.Length; i++) rightStates[i] = CharState.Extra;

            if (left.Length > common)
                details.AppendLine("Лишнее слева, с позиции " + (common + 1) + ": " + Quote(left.Substring(common)));
            if (right.Length > common)
                details.AppendLine("Лишнее справа, с позиции " + (common + 1) + ": " + Quote(right.Substring(common)));

            return new CompareResult
            {
                Left = left,
                Right = right,
                LeftStates = leftStates,
                RightStates = rightStates,
                Matches = matches,
                Mismatches = mismatches,
                Details = details.ToString().TrimEnd()
            };
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
            string visible = s.Replace("\n", "⏎").Replace("\t", "→");
            if (visible.Length > max) visible = visible.Substring(0, max) + "…";
            return "«" + visible + "»";
        }
    }
}
