using System;
using System.Text;
using System.Text.RegularExpressions;
using MultiTool.Models;

namespace MultiTool.Services
{
    public struct ExtractResult
    {
        public ExtractResult(bool success, string kind, string value, string error)
        {
            Success = success;
            Kind = kind;
            Value = value;
            Error = error;
        }

        public bool Success { get; }
        public string Kind { get; }
        public string Value { get; }
        public string Error { get; }
    }

    /// <summary>Определяет тип документа по тексту из буфера и достаёт нужный фрагмент (правила — в настройках).</summary>
    public static class ClipboardProcessor
    {
        private const char Marker = '№';

        /// <summary>Документ Accept: есть и «Отзыв документа», и «Заявление на акцепт» (проверяется раньше остальных правил).</summary>
        public static bool IsAcceptDocument(string text, AppSettings s)
        {
            return Matches(text, s.RevocationKeyword) && Matches(text, s.AcceptKeyword);
        }

        public static ExtractResult Extract(string text, AppSettings s)
        {
            if (Matches(text, s.RevocationKeyword))
                return Wrap("Отзыв документа", ExtractIban(text, s));

            if (Matches(text, s.StatementKeyword))
            {
                if (Matches(text, s.SuspendedKeyword))
                    return Wrap("Заявление (приостановление)",
                        ExtractAfterMarker(text, s, s.SuspendedOccurrence, s.SuspendedSkip, s.SuspendedLength));

                return Wrap("Заявление",
                    ExtractAfterMarker(text, s, s.StatementOccurrence, s.StatementSkip, s.StatementLength));
            }

            // Проверяется последним, чтобы не менять поведение «Заявления».
            if (Matches(text, s.OrderKeyword))
                return Wrap("Распоряжение",
                    ExtractAfterMarker(text, s, s.OrderOccurrence, s.OrderSkip, s.OrderLength));

            return new ExtractResult(false, "", "",
                "Тип документа не определён (нет «" + s.RevocationKeyword + "» / «" + s.StatementKeyword + "» / «" + s.OrderKeyword + "»)");
        }

        private static ExtractResult Wrap(string kind, Tuple<string, string> r)
        {
            return r.Item1 == null
                ? new ExtractResult(false, kind, "", r.Item2 ?? "")
                : new ExtractResult(true, kind, r.Item1, "");
        }

        private static Tuple<string, string> Ok(string value) { return Tuple.Create(value, (string)null); }

        private static Tuple<string, string> Fail(string error) { return Tuple.Create((string)null, error); }

        private static Tuple<string, string> ExtractIban(string text, AppSettings s)
        {
            Match label = BuildRegex(s.PayerAccountLabel).Match(text);
            if (!label.Success)
                return Fail("Не найден текст «" + s.PayerAccountLabel + "»");

            int by = text.IndexOf(s.IbanPrefix, label.Index + label.Length, StringComparison.Ordinal);
            if (by < 0)
                return Fail("После «" + s.PayerAccountLabel + "» нет «" + s.IbanPrefix + "»");

            int skip = Math.Max(0, s.IbanSkip), length = Math.Max(0, s.IbanLength);
            if (text.Length - by < skip + length)
                return Fail("После «" + s.IbanPrefix + "» слишком мало символов");

            return Ok(text.Substring(by + skip, length));
        }

        private static Tuple<string, string> ExtractAfterMarker(
            string text, AppSettings s, int occurrence, int skip, int length)
        {
            occurrence = Math.Max(1, occurrence);
            skip = Math.Max(0, skip);
            length = Math.Max(0, length);

            int idx = -1;
            for (int found = 0; found < occurrence; found++)
            {
                idx = text.IndexOf(Marker, idx + 1);
                if (idx < 0)
                {
                    return Fail(found == 0
                        ? "В буфере нет символа «" + Marker + "»"
                        : "В буфере только " + found + " симв. «" + Marker + "», а нужен №" + occurrence);
                }
            }

            string after = text.Substring(idx + 1);
            if (after.Length < s.TakeAfterMarker)
                return Fail("После «" + Marker + "» только " + after.Length + " симв., нужно " + s.TakeAfterMarker);

            string block = after.Substring(0, s.TakeAfterMarker);
            if (block.Length < skip + length)
                return Fail("В " + s.TakeAfterMarker + " символах после «" + Marker + "» не хватает места: пропуск " + skip + " + длина " + length);

            return Ok(block.Substring(skip, length));
        }

        private static bool Matches(string text, string keyword)
        {
            return !string.IsNullOrWhiteSpace(keyword) && BuildRegex(keyword).IsMatch(text);
        }

        /// <summary>Ключевая фраза → regex: без учёта регистра, пробелы = любые пробелы/переносы, е = ё.</summary>
        private static Regex BuildRegex(string keyword)
        {
            var sb = new StringBuilder();
            bool lastWasSpace = false;
            foreach (char c in keyword.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace) sb.Append(@"\s+");
                    lastWasSpace = true;
                    continue;
                }

                lastWasSpace = false;
                if (c == 'е' || c == 'ё' || c == 'Е' || c == 'Ё') sb.Append("[её]");
                else sb.Append(Regex.Escape(c.ToString()));
            }
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
