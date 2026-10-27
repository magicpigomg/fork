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
            // «ПЛАТЕЖНОЕ ПОРУЧЕНИЕ» + «Фактический бенефициар» — копируется код «ABOWD…».
            if (Matches(text, s.PaymentOrderKeyword) && Matches(text, s.ControlCodeKeyword))
                return Wrap("Платёжное поручение", ExtractControlCode(text, s));

            // «Заявление на отзыв платежной инструкции»: счёт плательщика берётся так же, как у «Отзыва документа».
            // Проверяется раньше «Заявления», иначе слово «Заявление» в заголовке сработало бы как другое правило.
            if (Matches(text, s.RevocationRequestKeyword))
                return Wrap("Заявление на отзыв", ExtractIban(text, s, s.RevocationRequestWholeAccount));

            if (Matches(text, s.RevocationKeyword))
                return Wrap("Отзыв документа", ExtractIban(text, s, false));

            if (Matches(text, s.StatementKeyword))
            {
                if (Matches(text, s.SuspendedKeyword))
                {
                    int occurrence = EffectiveOccurrence(text, s, s.SuspendedOccurrence, false);
                    return Wrap("Заявление (приостановление)" + OccurrenceNote(occurrence, s.SuspendedOccurrence),
                        ExtractAfterMarker(text, s, occurrence, s.SuspendedSkip, s.SuspendedLength));
                }

                return Wrap("Заявление",
                    ExtractAfterMarker(text, s, s.StatementOccurrence, s.StatementSkip, s.StatementLength));
            }

            // Проверяется последним, чтобы не менять поведение «Заявления».
            if (Matches(text, s.OrderKeyword))
            {
                int occurrence = EffectiveOccurrence(text, s, s.OrderOccurrence, true);
                return Wrap("Распоряжение" + OccurrenceNote(occurrence, s.OrderOccurrence),
                    ExtractAfterMarker(text, s, occurrence, s.OrderSkip, s.OrderLength));
            }

            return new ExtractResult(false, "", "",
                "Тип документа не определён (нет «" + s.RevocationKeyword + "» / «" + s.StatementKeyword + "» / «" + s.OrderKeyword + "»)");
        }

        /// <summary>
        /// Какой по счёту № брать. Обычно — как в настройках, но если в тексте три и более «№» (например, «Завод №1»)
        /// и правило берёт второй №, то берётся третий (число — в настройке OccurrenceWhenThreeMarkers).
        /// Исключение (только для «Распоряжения», allowBookingException) — распоряжение на бронирование денежных средств:
        /// там по-прежнему берётся второй №. В «Заявлении» эта фраза может быть лишь названием приостанавливаемого документа.
        /// </summary>
        private static int EffectiveOccurrence(string text, AppSettings s, int configured, bool allowBookingException)
        {
            if (configured == 2 && s.OccurrenceWhenThreeMarkers > configured && CountMarkers(text) >= 3
                && !(allowBookingException && Matches(text, s.BookingOrderKeyword)))
                return s.OccurrenceWhenThreeMarkers;
            return configured;
        }

        private static int CountMarkers(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (c == Marker) count++;
            }
            return count;
        }

        /// <summary>Пометка для статуса, если вместо обычного № взят другой.</summary>
        private static string OccurrenceNote(int used, int configured)
        {
            return used == configured ? "" : " (№" + used + ")";
        }

        private static ExtractResult Wrap(string kind, Tuple<string, string> r)
        {
            return r.Item1 == null
                ? new ExtractResult(false, kind, "", r.Item2 ?? "")
                : new ExtractResult(true, kind, r.Item1, "");
        }

        private static Tuple<string, string> Ok(string value) { return Tuple.Create(value, (string)null); }

        private static Tuple<string, string> Fail(string error) { return Tuple.Create((string)null, error); }

        private static Tuple<string, string> ExtractIban(string text, AppSettings s, bool wholeAccount)
        {
            Match label = BuildRegex(s.PayerAccountLabel).Match(text);
            if (!label.Success)
                return Fail("Не найден текст «" + s.PayerAccountLabel + "»");

            int by = text.IndexOf(s.IbanPrefix, label.Index + label.Length, StringComparison.Ordinal);
            if (by < 0)
                return Fail("После «" + s.PayerAccountLabel + "» нет «" + s.IbanPrefix + "»");

            // wholeAccount: от префикса берётся весь номер; иначе — только часть после банка (ALFA).
            int skip = wholeAccount ? 0 : Math.Max(0, s.IbanSkip);
            int length = Math.Max(0, wholeAccount ? s.IbanFullLength : s.IbanLength);
            if (text.Length - by < skip + length)
                return Fail("После «" + s.IbanPrefix + "» слишком мало символов");

            string value = text.Substring(by + skip, length);
            // Номер короче нужного: выбранные символы «зашли» бы на следующую строку документа.
            if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                return Fail("Номер счёта после «" + s.IbanPrefix + "» короче нужного (в строке меньше символов)");
            return Ok(value);
        }

        /// <summary>
        /// Код вида «ABOWD» + цифры до конца кода. Общая длина кода (вместе с префиксом) должна быть равна
        /// ControlCodeLength; если найденный код другой длины — ошибка с подробностями.
        /// </summary>
        private static Tuple<string, string> ExtractControlCode(string text, AppSettings s)
        {
            string prefix = (s.ControlCodePrefix ?? "").Trim();
            if (prefix.Length == 0)
                return Fail("Не задан префикс кода (Настройки → Правила разбора буфера)");

            MatchCollection matches = Regex.Matches(text, Regex.Escape(prefix) + @"\d+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (matches.Count == 0)
                return Fail("Не найден код «" + prefix + "» с цифрами");

            foreach (Match m in matches)
            {
                if (m.Value.Length == s.ControlCodeLength) return Ok(m.Value);
            }

            Match first = matches[0];
            return Fail("Найден код «" + first.Value + "» длиной " + first.Value.Length +
                        " симв., а нужно " + s.ControlCodeLength);
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
