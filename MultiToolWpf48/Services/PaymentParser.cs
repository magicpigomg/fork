using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MultiTool.Services
{
    /// <summary>Значения платежа, вытащенные из большого текста. Отсутствующее значение = null.</summary>
    public sealed class PaymentFields
    {
        public string Account { get; set; }           // счёт плательщика
        public string Amount { get; set; }            // сумма платежа с валютой
        public string RecipientAccount { get; set; }  // счёт получателя
        public string Swift { get; set; }             // SWIFT-код банка получателя
        public string Tnved { get; set; }             // ТНВЭД (может отсутствовать)
        public string RegNumber { get; set; }         // рег. № валютного договора (может отсутствовать)
        public string PurposeEn { get; set; }         // назначение платежа на английском

        /// <summary>Значения в порядке PaymentFieldInfo.Names.</summary>
        public string[] ToArray()
        {
            return new[] { Account, Amount, RecipientAccount, Swift, Tnved, RegNumber, PurposeEn };
        }

        public int FoundCount
        {
            get { return ToArray().Count(v => !string.IsNullOrEmpty(v)); }
        }
    }

    public static class PaymentFieldInfo
    {
        public static readonly string[] Names =
        {
            "Счёт", "Сумма платежа", "Счёт получателя", "SWIFT-код банка", "ТНВЭД", "Рег. №", "Назначение (англ.)"
        };

        /// <summary>Поля, которых в документе может не быть.</summary>
        public static readonly bool[] Optional = { false, false, false, false, true, true, false };
    }

    /// <summary>
    /// Достаёт значения платежа из двух видов текста: страницы сайта («Детализация платежа», метка и значение
    /// на соседних строках) и самого платёжного поручения (метки и значения в потоке текста).
    /// </summary>
    public static class PaymentParser
    {
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex AmountLineRx = new Regex(@"^\d[\d  ,\.]*\s*[A-Za-z]{3}$", Opt);
        private static readonly Regex SwiftRx = new Regex(@"^[A-Za-z]{6}[A-Za-z0-9]{2}([A-Za-z0-9]{3})?$", Opt);
        private static readonly Regex AccountRx = new Regex(@"^[A-Za-z0-9 ]{5,}$", Opt);
        private static readonly Regex TnvedRx = new Regex(@"^\d[\d,; ]*\d$", Opt);
        private static readonly Regex RegNumberRx = new Regex(@"^(?=.*\d)[\w/.\-]{5,}$", Opt);
        private static readonly Regex CyrillicRx = new Regex(@"[А-Яа-яЁё]", Opt);
        private static readonly Regex LatinRx = new Regex(@"[A-Za-z]", Opt);

        // Подписи полей на странице сайта: по ним понимаем, где заканчивается значение.
        private static readonly HashSet<string> SiteLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Комментарий", "Сумма к оплате", "Курс конверсии", "Назначение (ru)", "Назначение (eng)",
            "Очередность платежа", "Рег.№ валютного договора", "Дата отгрузки", "Тип оплаты", "ТНВЭД",
            "Категория", "Emails конт. лица", "Подразделение", "Контактное лицо", "Менеджер", "Сегмент",
            "Страна контрагента", "Наименование контрагента", "Адрес Банка", "Страна Банка", "Swift-Код Банка",
            "Банк-получатель", "Счет", "Адрес", "УНП/ИНН", "Страна Получателя", "Получатель", "Комиссия банка",
            "Сумма платежа", "Адрес (eng)", "Телефон", "Email", "УНП", "Базовый номер", "Отправитель",
            "Отправитель (eng)", "Дата создания"
        };

        // ───────────────────────── Страница сайта ─────────────────────────

        public static PaymentFields ParseSite(string text)
        {
            List<string> lines = SplitLines(text);
            var f = new PaymentFields();

            // Первый «Счет» — счёт отправителя, «Счет» после блока «Получатель» — счёт получателя.
            int sender = IndexOfLabel(lines, "^Отправитель$", 0);
            int recipient = IndexOfLabel(lines, "^Получатель$", 0);

            int payerLabel = IndexOfLabel(lines, "^Счет$", Math.Max(sender, 0));
            f.Account = ValueAt(lines, payerLabel, v => AccountRx.IsMatch(v));

            int recipientStart = recipient >= 0 ? recipient : (payerLabel >= 0 ? payerLabel + 1 : 0);
            int recipientLabel = IndexOfLabel(lines, "^Счет$", recipientStart);
            f.RecipientAccount = ValueAt(lines, recipientLabel, v => AccountRx.IsMatch(v));

            // «Сумма платежа» и «Комиссия банка» идут подряд, затем их значения — берём первую строку «число + валюта».
            int amountLabel = IndexOfLabel(lines, "^Сумма платежа$", 0);
            if (amountLabel >= 0)
            {
                for (int i = amountLabel + 1; i < lines.Count && i <= amountLabel + 8; i++)
                {
                    if (AmountLineRx.IsMatch(lines[i]))
                    {
                        f.Amount = NormalizeAmount(lines[i]);
                        break;
                    }
                }
            }

            f.Swift = ValueAt(lines, IndexOfLabel(lines, @"^swift[\s\-]*код\s+банка$", 0), v => SwiftRx.IsMatch(v));
            f.Tnved = ValueAt(lines, IndexOfLabel(lines, @"^ТН\s*ВЭД$", 0), v => TnvedRx.IsMatch(v));
            f.RegNumber = ValueAt(lines, IndexOfLabel(lines, @"^Рег\.?\s*№.*договор\w*$", 0), v => RegNumberRx.IsMatch(v));

            // Назначение: строки после метки, пока не начнётся следующая подпись.
            int purposeLabel = IndexOfLabel(lines, @"^Назначение\s*\(eng\)$", 0);
            if (purposeLabel >= 0)
            {
                var parts = new List<string>();
                for (int i = purposeLabel + 1; i < lines.Count; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0)
                    {
                        if (parts.Count > 0) break;
                        continue;
                    }
                    if (SiteLabels.Contains(line)) break;
                    parts.Add(line);
                }
                f.PurposeEn = parts.Count > 0 ? string.Join(" ", parts) : null;
            }

            return f;
        }

        // ───────────────────────── Платёжное поручение ─────────────────────────

        public static PaymentFields ParseDocument(string text)
        {
            string t = Normalize(text);
            var f = new PaymentFields();

            // «Счет №»: до слова «Бенефициар» — плательщик, после — получатель.
            int beneficiary = FirstIndex(t, @"Бенефициар");
            var accountRx = new Regex(@"Счет[ \t]*№[ \t]*:?[ \t]*([A-Za-z0-9]{5,})", Opt);
            List<Match> accounts = accountRx.Matches(t).Cast<Match>().ToList();
            foreach (Match m in accounts)
            {
                bool isPayer = beneficiary < 0 ? ReferenceEquals(m, accounts[0]) : m.Index < beneficiary;
                if (isPayer && f.Account == null) f.Account = m.Groups[1].Value;
                else if (!isPayer && f.RecipientAccount == null && (beneficiary < 0 || m.Index > beneficiary))
                    f.RecipientAccount = m.Groups[1].Value;
            }

            // Сумма цифрами + код валюты.
            Match amount = Regex.Match(t, @"Сумма[ \t]+цифрами[ \t]*:?[ \t]*(\d[\d  ,\.]*\d)", Opt);
            Match currency = Regex.Match(t, @"Код[ \t]+валюты[ \t]*:?[ \t]*([A-Za-z]{3})", Opt);
            if (amount.Success)
                f.Amount = NormalizeAmount(amount.Groups[1].Value + (currency.Success ? " " + currency.Groups[1].Value : ""));

            // SWIFT: первый «Код банка» после «Банк-получатель» (первый «Код банка» в документе — банк отправителя).
            int recipientBank = FirstIndex(t, @"Банк[ \t]*-[ \t]*получатель");
            if (recipientBank >= 0)
            {
                Match swift = Regex.Match(t.Substring(recipientBank), @"Код[ \t]+банка[ \t]*:?[ \t]*([A-Za-z0-9]{8,11})", Opt);
                if (swift.Success && SwiftRx.IsMatch(swift.Groups[1].Value)) f.Swift = swift.Groups[1].Value;
            }

            Match tnved = Regex.Match(t, @"Коды?[ \t]+ТН[ \t]*ВЭД[ \t]*:?[ \t]*(\d[\d,; \t]*\d)", Opt);
            if (tnved.Success) f.Tnved = tnved.Groups[1].Value.Trim();

            Match reg = Regex.Match(t,
                @"Регистрационный[ \t\n]+номер[ \t\n]+валютного[ \t\n]+договора[ \t]*:?[ \t]*([\w/.\-]*\d[\w/.\-]*)", Opt);
            if (reg.Success) f.RegNumber = reg.Groups[1].Value;

            f.PurposeEn = ExtractPurposeEnglish(t);
            return f;
        }

        /// <summary>После «Назначение платежа:» берутся строки до пустой строки; из них — только английский текст.</summary>
        private static string ExtractPurposeEnglish(string t)
        {
            Match label = Regex.Match(t, @"Назначение[ \t]+платежа[ \t]*:", Opt);
            if (!label.Success) return null;

            string[] lines = t.Substring(label.Index + label.Length).Split('\n');
            var parts = new List<string>();
            bool started = false;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    if (started) break;
                    continue;
                }

                // Следующая подпись документа («УНП плательщика: …») — назначение закончилось.
                if (started && Regex.IsMatch(line, @"^[А-Яа-яЁё][^:]{2,40}:", Opt)) break;
                started = true;

                string english = EnglishPart(line);
                if (english.Length > 0) parts.Add(english);
            }
            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }

        /// <summary>Строка целиком латинская — берём её; смешанная — берём латинский хвост после последней кириллической буквы.</summary>
        private static string EnglishPart(string line)
        {
            if (!LatinRx.IsMatch(line)) return "";
            MatchCollection cyr = CyrillicRx.Matches(line);
            if (cyr.Count == 0) return line;

            string tail = line.Substring(cyr[cyr.Count - 1].Index + 1).Trim(' ', '\t', '/', '-', '–', ',', ';', ':', '(', ')');
            return LatinRx.IsMatch(tail) ? tail : "";
        }

        // ───────────────────────── Вспомогательное ─────────────────────────

        private static string Normalize(string text)
        {
            return (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static List<string> SplitLines(string text)
        {
            return Normalize(text).Split('\n').Select(l => l.Trim()).ToList();
        }

        private static int IndexOfLabel(List<string> lines, string pattern, int start)
        {
            for (int i = Math.Max(0, start); i < lines.Count; i++)
            {
                if (Regex.IsMatch(lines[i], pattern, Opt)) return i;
            }
            return -1;
        }

        /// <summary>Значение — ближайшая непустая строка после метки; если она не подходит по формату (например,
        /// это уже следующая подпись, потому что значения нет), поле считается отсутствующим.</summary>
        private static string ValueAt(List<string> lines, int labelIndex, Func<string, bool> isValid)
        {
            if (labelIndex < 0) return null;
            for (int i = labelIndex + 1; i < lines.Count && i <= labelIndex + 3; i++)
            {
                if (lines[i].Length == 0) continue;
                return isValid(lines[i]) ? lines[i] : null;
            }
            return null;
        }

        private static int FirstIndex(string text, string pattern)
        {
            Match m = Regex.Match(text, pattern, Opt);
            return m.Success ? m.Index : -1;
        }

        /// <summary>Единый вид суммы: пробелы схлопнуты, валюта — заглавными, через один пробел от числа.</summary>
        private static string NormalizeAmount(string raw)
        {
            string s = Regex.Replace(raw.Replace(' ', ' ').Trim(), @"\s+", " ");
            Match m = Regex.Match(s, @"^(.*?)\s*([A-Za-z]{3})$");
            return m.Success ? m.Groups[1].Value.Trim() + " " + m.Groups[2].Value.ToUpperInvariant() : s;
        }
    }
}
