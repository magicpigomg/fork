using System;
using MultiTool.Models;

namespace MultiTool.Services
{
    /// <summary>Логика бывших форм Form2 (очистка пробелов) и Form3 (разбор номера).</summary>
    public static class TextTools
    {
        /// <summary>Убирает обычные и неразрывные пробелы.</summary>
        public static string RemoveSpaces(string text)
        {
            return text.Replace(" ", "").Replace(" ", "").Replace(" ", "");
        }

        public struct NumberParts
        {
            public NumberParts(string first, string second, string third, bool complete)
            {
                First = first;
                Second = second;
                Third = third;
                Complete = complete;
            }

            public string First { get; }
            public string Second { get; }
            public string Third { get; }
            public bool Complete { get; }
        }

        /// <summary>
        /// Результат 1 = без пробелов, убрать первые N, оставить M.
        /// Результат 2 = результат 1 без первых K. Результат 3 = первые L символов результата 2.
        /// В исходной форме при короткой строке была ошибка; здесь берётся сколько есть, а Complete = false.
        /// </summary>
        public static NumberParts ParseNumber(string text, AppSettings s)
        {
            string clean = RemoveSpaces(text);

            string first = Take(Skip(clean, s.NumberSkipFirst), s.NumberTake);
            string second = Skip(first, s.NumberSkipSecond);
            string third = Take(second, s.NumberTakeThird);

            bool complete = clean.Length >= Math.Max(0, s.NumberSkipFirst) + Math.Max(0, s.NumberTake)
                            && second.Length >= Math.Max(0, s.NumberTakeThird);
            return new NumberParts(first, second, third, complete);
        }

        private static string Skip(string s, int count)
        {
            return count <= 0 ? s : count >= s.Length ? "" : s.Substring(count);
        }

        private static string Take(string s, int count)
        {
            return count <= 0 ? "" : count >= s.Length ? s : s.Substring(0, count);
        }
    }
}
