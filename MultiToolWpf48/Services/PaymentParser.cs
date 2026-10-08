using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MultiTool.Services
{
    /// <summary>Значения платежа, вытащенные из большого текста. Отсутствующее значение = null.</summary>
    public sealed class PaymentFields
    {
        public string Number { get; set; }            // номер платежа / платёжного поручения
        public string Date { get; set; }              // дата ПП
        public string Account { get; set; }           // счёт плательщика
        public string Amount { get; set; }            // сумма платежа с валютой
        public string Recipient { get; set; }         // получатель: имя, адрес, страна
        public string RecipientAccount { get; set; }  // счёт получателя
        public string RecipientBank { get; set; }     // банк получателя: страна и название, адрес
        public string Swift { get; set; }             // SWIFT-код банка получателя
        public string Tnved { get; set; }             // ТНВЭД (может отсутствовать)
        public string RegNumber { get; set; }         // рег. № валютного договора (может отсутствовать)
        public string PurposeEn { get; set; }         // назначение платежа на английском

        /// <summary>Значения в порядке PaymentFieldInfo.Names.</summary>
        public string[] ToArray()
        {
            return new[]
            {
                Number, Date, Account, Amount, Recipient, RecipientAccount, RecipientBank, Swift, Tnved, RegNumber, PurposeEn
            };
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
            "№ платежа (ПП)", "Дата ПП", "Счёт", "Сумма платежа", "Получатель", "Счёт получателя",
            "Банк получателя", "SWIFT-код банка", "ТНВЭД", "Рег. №", "Назначение (англ.)"
        };

        /// <summary>Поля, которых в документе может не быть.</summary>
        public static readonly bool[] Optional = { false, false, false, false, false, false, false, false, true, true, false };
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
            string t = Normalize(text);
            var f = new PaymentFields();

            // «Детализация платежа № 5663» и «Дата ПП 08.10.2026» (значение может быть и на следующей строке).
            Match number = Regex.Match(t, @"Детализация[ \t]+платежа[ \t]*№[ \t]*(\d[\w/\-]*)", Opt);
            if (number.Success) f.Number = number.Groups[1].Value;

            // \s в .NET включает неразрывный пробел и перенос строки; допускаем и двоеточие после метки.
            Match date = Regex.Match(t, @"Дата\s+ПП\s*:?\s*(\d{1,2}\.\d{1,2}\.\d{4})", Opt);
            if (date.Success) f.Date = date.Groups[1].Value;

            // Первый «Счет» — счёт отправителя, «Счет» после блока «Получатель» — счёт получателя.
            int sender = IndexOfLabel(lines, "^Отправитель$", 0);
            int recipient = IndexOfLabel(lines, "^Получатель$", 0);

            int payerLabel = IndexOfLabel(lines, "^Счет$", Math.Max(sender, 0));
            f.Account = ValueAt(lines, payerLabel, v => AccountRx.IsMatch(v));

            int recipientStart = recipient >= 0 ? recipient : (payerLabel >= 0 ? payerLabel + 1 : 0);
            int recipientLabel = IndexOfLabel(lines, "^Счет$", recipientStart);
            f.RecipientAccount = ValueAt(lines, recipientLabel, v => AccountRx.IsMatch(v));

            // Получатель: имя, адрес, страна — через запятую.
            if (recipient >= 0)
            {
                string name = ValueAt(lines, recipient, IsNotLabel);
                string address = ValueAt(lines, IndexOfLabel(lines, "^Адрес$", recipient), IsNotLabel);
                string country = ValueAt(lines, IndexOfLabel(lines, @"^Страна\s+Получателя$", recipient), IsNotLabel);
                f.Recipient = JoinNonEmpty(", ", name, address, country);
            }

            // Банк получателя: «страна банка пробел банк-получатель», затем через запятую адрес банка.
            int bankLabel = IndexOfLabel(lines, @"^Банк[\s\-]*получатель$", 0);
            if (bankLabel >= 0)
            {
                string bank = ValueAt(lines, bankLabel, IsNotLabel);
                string bankCountry = ValueAt(lines, IndexOfLabel(lines, @"^Страна\s+Банка$", bankLabel), IsNotLabel);
                string bankAddress = ValueAt(lines, IndexOfLabel(lines, @"^Адрес\s+Банка$", bankLabel), IsNotLabel);
                f.RecipientBank = JoinNonEmpty(", ", JoinNonEmpty(" ", bankCountry, bank), bankAddress);
            }

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

            // «ПЛАТЕЖНОЕ ПОРУЧЕНИЕ № 5563 Дата 08.10.2026»
            Match number = Regex.Match(t, @"ПЛАТЕЖНОЕ[ \t]+ПОРУЧЕНИЕ[ \t]*№[ \t]*(\d[\w/\-]*)", Opt);
            if (number.Success) f.Number = number.Groups[1].Value;

            // Дата после слова «Дата»: допускаем пробелы любого вида, перенос строки и двоеточие (\s включает неразрывный пробел).
            const string dateRx = @"Дата\s*:?\s*(\d{1,2}\.\d{1,2}\.\d{4})";
            Match date = Regex.Match(t, dateRx, Opt);
            if (number.Success)
            {
                Match afterNumber = Regex.Match(t.Substring(number.Index), dateRx, Opt);
                if (afterNumber.Success) date = afterNumber;
            }
            if (date.Success) f.Date = date.Groups[1].Value;

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

            // Получатель — всё между «Бенефициар:» и «Счет №» (в документе он может занимать несколько строк).
            Match beneficiaryLabel = Regex.Match(t, @"Бенефициар[ \t]*:", Opt);
            if (beneficiaryLabel.Success)
            {
                string rest = t.Substring(beneficiaryLabel.Index + beneficiaryLabel.Length);
                Match stop = Regex.Match(rest, @"Счет[ \t]*№", Opt);
                string chunk = stop.Success ? rest.Substring(0, stop.Index) : rest.Split(new[] { "\n\n" }, StringSplitOptions.None)[0];
                f.Recipient = CollapseSpaces(chunk);
            }

            // Банк получателя — всё между «Банк-получатель:» и следующим «Код банка».
            Match bankLabel = Regex.Match(t, @"Банк[ \t]*-[ \t]*получатель[ \t]*:", Opt);
            if (bankLabel.Success)
            {
                string rest = t.Substring(bankLabel.Index + bankLabel.Length);
                Match stop = Regex.Match(rest, @"Код[ \t]+банка", Opt);
                if (stop.Success) f.RecipientBank = CollapseSpaces(rest.Substring(0, stop.Index));

                // SWIFT — первый «Код банка» после «Банк-получатель» (первый «Код банка» в документе — банк отправителя).
                Match swift = Regex.Match(rest, @"Код[ \t]+банка[ \t]*:?[ \t]*([A-Za-z0-9]{8,11})", Opt);
                if (swift.Success && SwiftRx.IsMatch(swift.Groups[1].Value)) f.Swift = swift.Groups[1].Value;
            }

            // Сумма цифрами + код валюты.
            Match amount = Regex.Match(t, @"Сумма[ \t]+цифрами[ \t]*:?[ \t]*(\d[\d  ,\.]*\d)", Opt);
            Match currency = Regex.Match(t, @"Код[ \t]+валюты[ \t]*:?[ \t]*([A-Za-z]{3})", Opt);
            if (amount.Success)
                f.Amount = NormalizeAmount(amount.Groups[1].Value + (currency.Success ? " " + currency.Groups[1].Value : ""));

            Match tnved = Regex.Match(t, @"Коды?[ \t]+ТН[ \t]*ВЭД[ \t]*:?[ \t]*(\d[\d,; \t]*\d)", Opt);
            if (tnved.Success) f.Tnved = tnved.Groups[1].Value.Trim();

            Match reg = Regex.Match(t,
                @"Регистрационный[ \t\n]+номер[ \t\n]+валютного[ \t\n]+договора[ \t]*:?[ \t]*([\w/.\-]*\d[\w/.\-]*)", Opt);
            if (reg.Success) f.RegNumber = reg.Groups[1].Value;

            f.PurposeEn = ExtractPurposeEnglish(t);
            return f;
        }

        /// <summary>
        /// «Назначение платежа: …» — первая строка (остаток строки с меткой) это русский текст, в ней могут быть
        /// латинские символы и даты, поэтому она не учитывается. Английское назначение — следующие строки до пустой
        /// строки или до следующей подписи; строка с кириллицей английским назначением не считается.
        /// </summary>
        private static string ExtractPurposeEnglish(string t)
        {
            Match label = Regex.Match(t, @"Назначение[ \t]+платежа[ \t]*:", Opt);
            if (!label.Success) return null;

            string[] lines = t.Substring(label.Index + label.Length).Split('\n');

            // Остаток строки с меткой — русская часть, пропускаем её (даже если там есть латиница).
            int start = lines[0].Trim().Length > 0 ? 1 : 0;
            bool russianSkipped = start == 1; // если после метки пусто, русский текст — первая непустая строка ниже

            var parts = new List<string>();
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    if (parts.Count > 0) break;
                    continue;
                }

                if (!russianSkipped)
                {
                    russianSkipped = true;
                    if (CyrillicRx.IsMatch(line)) continue;
                }

                // Следующая подпись документа («УНП плательщика: …») или русская строка — назначение закончилось.
                if (CyrillicRx.IsMatch(line)) break;
                if (!LatinRx.IsMatch(line)) break;
                parts.Add(line);
            }
            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }

        // ───────────────────────── Вспомогательное ─────────────────────────

        private static string Normalize(string text)
        {
            return (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string CollapseSpaces(string s)
        {
            return Regex.Replace(s.Replace(' ', ' '), @"\s+", " ").Trim();
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

        private static bool IsNotLabel(string value)
        {
            return !SiteLabels.Contains(value);
        }

        private static string JoinNonEmpty(string separator, params string[] parts)
        {
            string joined = string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return joined.Length > 0 ? joined : null;
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
