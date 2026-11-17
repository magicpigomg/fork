using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MultiTool.Services
{
    public enum CreditKind
    {
        /// <summary>Курс банка берётся из документа: макросы !80 и !70 (бывшая кнопка «с курсом»).</summary>
        WithRate,
        /// <summary>Курса в документе нет, сумма пересчитывается по курсу банка из таблицы: макросы !90 и !70.</summary>
        NoRate,
        /// <summary>Конвертация: сумма и курс берутся из документа: макросы !90 и !70.</summary>
        Conversion
    }

    public sealed class CreditMacroFile
    {
        public CreditMacroFile(string fileName, string text)
        {
            FileName = fileName;
            Text = text;
        }

        /// <summary>Предлагаемое имя файла без расширения («!80», «!90», «!70»).</summary>
        public string FileName { get; private set; }
        public string Text { get; private set; }
    }

    public sealed class CreditBuildResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = "";
        public string Summary { get; set; } = "";
        public List<CreditMacroFile> Files { get; set; } = new List<CreditMacroFile>();
    }

    /// <summary>
    /// Макросы .mac для эмулятора по платёжному поручению из буфера обмена (перенос трёх сценариев программы Credit).
    /// Документ разбирается по номерам строк (с 0), как в исходной программе: 1 — номер и дата, 2 — сумма и валюта, 6 — плательщик,
    /// 7 — счёт плательщика, 13 — счёт получателя, 16 — назначение платежа, 18 — очередь платежа.
    /// </summary>
    public static class CreditMacro
    {
        private const string Tab = "[tab field]";
        private const string Crlf = "\r\n";
        private const string Bank = "0073306";

        // Счета получателя по валюте платежа.
        private const string AccountUsd = "3819823389840";
        private const string AccountEur = "3819823380978";
        private const string AccountRub = "3819823389810";

        private sealed class CreditException : Exception
        {
            public CreditException(string message) : base(message) { }
        }

        /// <summary>Значения, которые одинаково берутся из документа во всех трёх сценариях.</summary>
        private sealed class Common
        {
            public string NumberDoc, AmountAndCurrency, Currency, AccountBen, PayerAccount, Purpose, PayerName, Queue, BenAccount;
            public double CursNbrb;
        }

        public static CreditBuildResult Build(CreditKind kind, string clipboardText, CreditRateSet rates)
        {
            if (string.IsNullOrWhiteSpace(clipboardText))
                return Fail("Буфер обмена пуст: скопируйте платёжное поручение и повторите");

            try
            {
                List<string> list = SplitLines(clipboardText);
                switch (kind)
                {
                    case CreditKind.WithRate: return BuildWithRate(list, rates);
                    case CreditKind.NoRate: return BuildNoRate(list, rates);
                    default: return BuildConversion(list, rates);
                }
            }
            catch (CreditException ex)
            {
                return Fail(ex.Message);
            }
            catch (Exception ex)
            {
                return Fail("Не удалось разобрать документ: " + ex.Message);
            }
        }

        // ── С курсом: !80 и !70 ──────────────────────────────────────────────────────────────

        private static CreditBuildResult BuildWithRate(List<string> list, CreditRateSet rates)
        {
            Common c = ReadCommon(list, rates, "[^0-9A-Z]", false);

            if (c.AmountAndCurrency.Length < 50)
                throw new CreditException("Строка с суммой короче 50 символов — документ скопирован не полностью");
            string sum = Regex.Replace(c.AmountAndCurrency.Substring(0, 50), "[^0-9]", "");
            if (sum.Length == 0) throw new CreditException("В строке с суммой нет цифр");

            int colon = c.AmountAndCurrency.LastIndexOf(':');
            if (colon < 0) throw new CreditException("В строке с суммой нет «:» — не найден курс банка");
            string cursAlfa = Regex.Replace(c.AmountAndCurrency.Substring(colon), "[^0-9.,]", "");
            if (cursAlfa.EndsWith(".")) cursAlfa = cursAlfa.Substring(0, cursAlfa.Length - 1);
            if (cursAlfa.Length == 0) throw new CreditException("После последнего «:» в строке с суммой нет курса банка");

            string curs = CursText(c.CursNbrb);

            // Первая проводка: обычно 80-я; при очереди 05 — 90-я (как в «Конвертации», курс банка берётся из документа).
            bool queue05 = IsQueue05(c.Queue);
            CreditMacroFile first;
            string queueNote = "";
            if (queue05)
            {
                decimal sumUnits;
                if (!decimal.TryParse(sum, NumberStyles.None, CultureInfo.InvariantCulture, out sumUnits))
                    throw new CreditException("Не удалось прочитать сумму: «" + sum + "»");
                double sumValue = (double)(sumUnits / 100m);   // цифры суммы в документе идут без разделителя, последние две — копейки
                double rate = ParseNumber(cursAlfa.Replace(",", "."), "курс банка из документа");
                double summa = Math.Round(sumValue * rate, 2);
                first = File("!90", Posting90(c, summa, sumValue));
                queueNote = "; очередь 05 → !90, сумма " + F2(sumValue) + " × курс " + cursAlfa + " = " + F2(summa);
            }
            else
            {
                var m80 = Head("80", c.NumberDoc);
                Repeat(m80, Tab, 3);
                m80.Add(Q(c.AccountBen));
                m80.Add(Tab);
                m80.Add(Q(sum));
                m80.Add(Tab);
                m80.Add(Q(cursAlfa));
                m80.Add(Tab);
                m80.Add(Q(c.PayerAccount));
                m80.AddRange(new[] { "[enter]", "[pf24]", "[enter]", Tab });
                m80.Add(Q(c.Purpose + " " + c.PayerName));
                m80.Add("[pf5]");
                Repeat(m80, "[up]", 10);
                m80.Add(Q(c.Queue));
                m80.Add("[enter]");
                m80.Add(Q(Bank));
                m80.AddRange(new[] { "[pf12]", "[pf12]" });
                first = File("!80", m80);
            }

            // 70-я проводка
            var m70 = Head("70", c.NumberDoc);
            Repeat(m70, Tab, 3);
            m70.Add(Q(c.AccountBen));
            m70.Add(Tab);
            m70.Add(Q(sum));
            m70.Add(Tab);
            m70.Add(Q(curs));
            m70.Add(Tab);
            m70.Add(Q(c.BenAccount));
            m70.AddRange(new[] { "[enter]", Tab });
            m70.Add(Q(c.Purpose + " " + c.PayerName));
            m70.Add("[enter]");
            m70.Add(Q(Bank));
            m70.AddRange(new[] { "[pf12]", "[pf12]" });

            return Ok("№" + c.NumberDoc + ", " + c.Currency + ", сумма " + sum + ", курс банка " + cursAlfa + queueNote,
                first, File("!70", m70));
        }

        // ── Без курса: !90 и !70 ─────────────────────────────────────────────────────────────

        private static CreditBuildResult BuildNoRate(List<string> list, CreditRateSet rates)
        {
            Common c = ReadCommon(list, rates, "[^0-9-A-Z]", true);

            if (c.AmountAndCurrency.Length < 40)
                throw new CreditException("Строка с суммой короче 40 символов — документ скопирован не полностью");
            string sum = Regex.Replace(c.AmountAndCurrency.Substring(0, 40), "[^0-9.]", "");
            double sumNumber = ParseNumber(sum, "сумму платежа");

            int alfaIndex = c.Currency == "USD" ? 0 : (c.Currency == "EUR" ? 1 : 2);
            string alfaText = rates.AlfaLine(alfaIndex);
            if (string.IsNullOrWhiteSpace(alfaText))
                throw new CreditException("Нет курса банка для " + c.Currency + ": нажмите «Обновить курсы» (или заполните rates_alfa.txt)");
            double alfa = ParseNumber(alfaText.Trim(), "курс банка");

            double summaValue = c.Currency == "RUB"
                ? Math.Round(alfa / 100 * sumNumber, 2)
                : Math.Round(alfa * sumNumber, 2);
            string summa = F2(summaValue);
            string curs = CursText(c.CursNbrb);

            // 90-я проводка
            var m90 = Head("90", c.NumberDoc);
            Repeat(m90, Tab, 3);
            m90.Add(Q(c.PayerAccount));
            m90.Add(Q(summa));
            m90.Add(Tab);
            m90.Add(Q(c.AccountBen));
            m90.Add(Tab);
            m90.Add(Q(sum));
            m90.AddRange(new[] { "[enter]", Tab });
            m90.Add(Q(c.Purpose + " " + c.PayerName));
            m90.Add("[home]");
            Repeat(m90, Tab, 9);
            m90.Add(Q(c.Queue));
            m90.AddRange(new[] { "[enter]", "[pf5]" });
            m90.Add(Q(Bank));
            m90.AddRange(new[] { "[pf12]", "[pf12]" });

            // 70-я проводка
            var m70 = Head("70", c.NumberDoc);
            Repeat(m70, Tab, 3);
            m70.Add(Q(c.AccountBen));
            m70.Add(Tab);
            m70.Add(Q(sum));
            m70.Add(Tab);
            m70.Add(Q(curs));
            m70.Add(Tab);
            m70.Add(Q(c.BenAccount));
            m70.AddRange(new[] { "[enter]", Tab });
            m70.Add(Q(c.Purpose + " " + c.PayerName));
            m70.Add("[enter]");
            m70.Add(Q(Bank));
            m70.AddRange(new[] { "[pf12]", "[pf12]" });

            return Ok("№" + c.NumberDoc + ", " + c.Currency + ", сумма " + sum + " → " + summa + " по курсу банка " + alfaText.Trim(),
                File("!90", m90), File("!70", m70));
        }

        // ── Конвертация: !90 и !70 ───────────────────────────────────────────────────────────

        private static CreditBuildResult BuildConversion(List<string> list, CreditRateSet rates)
        {
            Common c = ReadCommon(list, rates, "[^0-9A-Z]", false);

            string amount = c.AmountAndCurrency;
            if (amount.EndsWith(".")) amount = amount.Substring(0, amount.Length - 1);   // точка в конце курса

            string[] temp = amount.Split(' ');
            if (temp.Length <= 8)
                throw new CreditException("В строке с суммой меньше 9 слов, разделённых пробелами: не найти сумму документа");

            double sum = ParseNumber(temp[8].Replace(",", "."), "сумму документа");
            string oldCurs = F2(ParseNumber(temp[temp.Length - 1].Replace(",", "."), "курс из документа"));
            double curs = double.Parse(oldCurs, CultureInfo.InvariantCulture);
            double summa = Math.Round(sum * curs, 2);
            string cursNbrb = CursText(c.CursNbrb);

            // 90-я проводка
            List<string> m90 = Posting90(c, summa, sum);

            // 70-я проводка
            var m70 = Head("70", c.NumberDoc);
            Repeat(m70, Tab, 3);
            m70.Add(Q(c.AccountBen));
            m70.Add(Tab);
            m70.Add(Q(F2(sum)));
            m70.Add(Tab);
            m70.Add(Q(cursNbrb));
            m70.Add(Tab);
            m70.Add(Q(c.BenAccount));
            m70.AddRange(new[] { "[enter]", Tab });
            m70.Add(Q(c.Purpose + " " + c.PayerName));
            m70.Add("[enter]");
            m70.Add(Q(Bank));
            m70.AddRange(new[] { "[pf12]", "[pf12]" });

            return Ok("№" + c.NumberDoc + ", " + c.Currency + ", сумма " + F2(sum) + " × курс " + oldCurs + " = " + F2(summa),
                File("!90", m90), File("!70", m70));
        }

        // ── Общий разбор документа ───────────────────────────────────────────────────────────

        private static Common ReadCommon(List<string> list, CreditRateSet rates, string benAccountPattern, bool rubNbrbPerUnit)
        {
            var c = new Common();

            // Номер платёжного поручения: цифры строки 1 до последнего слова «Дата».
            string line1 = Line(list, 1);
            int dateAt = line1.LastIndexOf("Дата", StringComparison.Ordinal);
            if (dateAt < 0)
                throw new CreditException("Не найден номер платёжного поручения: во второй строке документа нет слова «Дата»");
            c.NumberDoc = Regex.Replace(line1.Substring(0, dateAt), "[^0-9]", "");
            if (c.NumberDoc.Length == 0)
                throw new CreditException("Не найден номер платёжного поручения: во второй строке перед словом «Дата» нет цифр");

            // Сумма и валюта; по валюте выбирается счёт получателя и курс НБ РБ.
            c.AmountAndCurrency = Line(list, 2);
            if (c.AmountAndCurrency.Contains("USD")) { c.Currency = "USD"; c.AccountBen = AccountUsd; }
            else if (c.AmountAndCurrency.Contains("EUR")) { c.Currency = "EUR"; c.AccountBen = AccountEur; }
            else { c.Currency = "RUB"; c.AccountBen = AccountRub; }

            double? nbrb = rates.NbrbRate(c.Currency);
            if (nbrb == null)
                throw new CreditException("Нет курса НБ РБ для " + c.Currency + ": нажмите «Обновить курсы»");
            // Для российского рубля в сценарии «без курса» курс НБ РБ (за 100 руб.) приводится к одному рублю.
            c.CursNbrb = c.Currency == "RUB" && rubNbrbPerUnit ? nbrb.Value / 100 : nbrb.Value;

            // Счёт плательщика: без подписи «Счёт №» и без пробелов.
            c.PayerAccount = Regex.Replace(Line(list, 7), "\tСч[её]т\\s?[^\\t\\s]?\t", "").Replace(" ", "");

            c.Purpose = Line(list, 16).Replace("Назначение платежа: \t", "").Trim().Replace("Назначение платежа: ", "");

            string payer = Line(list, 6);
            c.PayerName = payer.Contains(",")
                ? payer.Remove(payer.IndexOf(",", StringComparison.Ordinal)).Replace("Плательщик: ", "")
                : payer.Replace("Плательщик:", "").Trim();

            // Счёт получателя: 13 символов после первых 8 (код банка).
            string benAccount = Regex.Replace(Line(list, 13), benAccountPattern, "");
            if (benAccount.Length < 21)
                throw new CreditException("Счёт получателя (строка 14) короче 21 символа: «" + benAccount + "»");
            c.BenAccount = benAccount.Substring(8, 13);

            // Очередь платежа: цифры строки 18 без префикса (9 или 18 символов, по длине).
            string queue = Regex.Replace(Line(list, 18), "[^0-9]", "");
            int cut = queue.Length <= 11 ? 9 : 18;
            if (queue.Length < cut)
                throw new CreditException("Очередь платежа (строка 19) короче ожидаемого: «" + queue + "»");
            c.Queue = queue.Substring(cut);

            return c;
        }

        /// <summary>
        /// 90-я проводка с суммой в рублях (summa) и суммой в валюте (sum): курс банка берётся из документа.
        /// Используется в «Конвертации» и в «С курсом» при очереди 05.
        /// </summary>
        private static List<string> Posting90(Common c, double summa, double sum)
        {
            var m90 = Head("90", c.NumberDoc);
            Repeat(m90, Tab, 3);
            m90.Add(Q(c.PayerAccount));
            m90.Add(Q(F2(summa)));
            m90.Add(Tab);
            m90.Add(Q(c.AccountBen));
            m90.Add(Tab);
            m90.Add(Q(F2(sum)));
            m90.AddRange(new[] { "[enter]", "[pf24]", "[enter]", Tab });
            m90.Add(Q(c.Purpose + " " + c.PayerName));
            m90.Add("[pf5]");
            Repeat(m90, "[up]", 13);
            m90.Add(Q(c.Queue));
            m90.AddRange(new[] { "[enter]", "[pf5]" });
            m90.Add(Q(Bank));
            m90.AddRange(new[] { "[pf12]", "[pf12]" });
            return m90;
        }

        /// <summary>Очередь платежа 05: значение «5» с любыми ведущими нулями («05», «5», «005»).</summary>
        private static bool IsQueue05(string queue)
        {
            return queue != null && queue.TrimStart('0') == "5";
        }

        // ── Мелочи ───────────────────────────────────────────────────────────────────────────

        /// <summary>Строки как у File.ReadLines: разделители \r\n, \n, \r; последний пустой элемент после перевода строки отбрасывается.</summary>
        private static List<string> SplitLines(string text)
        {
            var lines = new List<string>(Regex.Split(text, "\r\n|\r|\n"));
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        private static string Line(List<string> list, int index)
        {
            if (index >= list.Count)
                throw new CreditException("В документе нет строки №" + (index + 1) + " (всего " + list.Count + ") — скопируйте платёжное поручение целиком");
            return list[index];
        }

        private static double ParseNumber(string text, string what)
        {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new CreditException("Не удалось прочитать " + what + ": «" + text + "»");
            return value;
        }

        private static string F2(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>Курс НБ РБ в макрос: как double.ToString() с точкой.</summary>
        private static string CursText(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Строка макроса «ввести текст» — значение с кавычкой в начале.</summary>
        private static string Q(string value)
        {
            return "\"" + value;
        }

        private static List<string> Head(string type, string numberDoc)
        {
            return new List<string>
            {
                "Description =", "[home]", Q("YI1"), "[enter]", Q(type), "[enter]", Q("06"), Q(numberDoc), "[erase eof]"
            };
        }

        private static void Repeat(List<string> lines, string line, int count)
        {
            for (int i = 0; i < count; i++) lines.Add(line);
        }

        private static CreditMacroFile File(string name, List<string> lines)
        {
            return new CreditMacroFile(name, string.Join(Crlf, lines));
        }

        private static CreditBuildResult Ok(string summary, params CreditMacroFile[] files)
        {
            return new CreditBuildResult { Success = true, Summary = summary, Files = new List<CreditMacroFile>(files) };
        }

        private static CreditBuildResult Fail(string error)
        {
            return new CreditBuildResult { Success = false, Error = error };
        }
    }
}
