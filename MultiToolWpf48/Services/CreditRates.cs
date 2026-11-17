using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MultiTool.Models;

namespace MultiTool.Services
{
    /// <summary>Курс НБ РБ в том виде, как его отдаёт api.nbrb.by (parammode=2); файл rates_nbrb.json совместим со старой программой.</summary>
    [DataContract]
    public sealed class NbrbRate
    {
        [DataMember(Name = "Cur_ID")] public int Id { get; set; }
        [DataMember(Name = "Cur_Name")] public string Name { get; set; }
        [DataMember(Name = "Cur_Abbreviation")] public string Abbreviation { get; set; }
        [DataMember(Name = "Cur_Scale")] public int Scale { get; set; }
        [DataMember(Name = "Cur_OfficialRate")] public double OfficialRate { get; set; }
    }

    /// <summary>Сохранённые курсы: НБ РБ (rates_nbrb.json) и банка (rates_alfa.txt — три строки: USD, EUR, RUB).</summary>
    public sealed class CreditRateSet
    {
        public List<NbrbRate> Nbrb { get; set; } = new List<NbrbRate>();
        public string[] Alfa { get; set; } = new string[0];
        public DateTime? NbrbUpdated { get; set; }
        public DateTime? AlfaUpdated { get; set; }

        /// <summary>Курс НБ РБ по коду валюты («USD», «EUR», «RUB») или null, если его нет.</summary>
        public double? NbrbRate(string abbreviation)
        {
            double? found = null;
            foreach (NbrbRate r in Nbrb)
            {
                if (r != null && r.Abbreviation == abbreviation) found = r.OfficialRate;   // как в исходной программе: побеждает последний
            }
            return found;
        }

        /// <summary>Курс банка по порядковому номеру строки (0 — USD, 1 — EUR, 2 — RUB), как текст из файла.</summary>
        public string AlfaLine(int index)
        {
            return index >= 0 && index < Alfa.Length ? Alfa[index] : null;
        }
    }

    public sealed class CreditRatesUpdate
    {
        public bool NbrbOk { get; set; }
        public bool AlfaOk { get; set; }
        public string NbrbMessage { get; set; } = "";
        public string AlfaMessage { get; set; } = "";
        public bool Success { get { return NbrbOk && AlfaOk; } }
    }

    /// <summary>
    /// Загрузка и хранение курсов для кредитовых макросов. Без внешних библиотек: JSON — DataContractJsonSerializer,
    /// таблица курсов банка — вырезка элемента по id и снятие тегов (то же, что делал HtmlAgilityPack через InnerText).
    /// </summary>
    public static class CreditRates
    {
        private static readonly string[] NbrbUrls =
        {
            "https://api.nbrb.by/exrates/rates/USD?parammode=2",
            "https://api.nbrb.by/exrates/rates/EUR?parammode=2",
            "https://api.nbrb.by/exrates/rates/RUB?parammode=2"
        };

        private const string BankTableId = "tblRatesH3";
        private const int BankTextSkip = 97;       // сколько символов текста таблицы отбрасывается до курсов
        private const int BankRateLength = 6;      // длина одного курса в тексте
        private static readonly int[] BankRateOffsets = { 6, 18, 30 };   // USD, EUR, RUB — смещения после пропуска

        private const string NbrbFile = "rates_nbrb.json";
        private const string AlfaFile = "rates_alfa.txt";

        /// <summary>Папка с файлами курсов: из настроек («Кредит»), а пока она не выбрана — папка настроек программы.</summary>
        public static string Folder
        {
            get
            {
                string chosen = AppSettings.Current != null ? AppSettings.Current.CreditRatesFolder : null;
                return string.IsNullOrWhiteSpace(chosen) ? SettingsStore.Folder : chosen.Trim();
            }
        }

        public static string NbrbPath { get { return Path.Combine(Folder, NbrbFile); } }
        public static string AlfaPath { get { return Path.Combine(Folder, AlfaFile); } }

        /// <summary>Читает сохранённые курсы; отсутствующий или битый файл даёт пустой набор, а не исключение.</summary>
        public static CreditRateSet Load()
        {
            var set = new CreditRateSet();
            try
            {
                if (File.Exists(NbrbPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<NbrbRate>));
                    using (var stream = new MemoryStream(File.ReadAllBytes(NbrbPath)))
                        set.Nbrb = (List<NbrbRate>)serializer.ReadObject(stream) ?? new List<NbrbRate>();
                    set.NbrbUpdated = File.GetLastWriteTime(NbrbPath);
                }
            }
            catch (Exception) { set.Nbrb = new List<NbrbRate>(); }

            try
            {
                if (File.Exists(AlfaPath))
                {
                    set.Alfa = File.ReadAllLines(AlfaPath);
                    set.AlfaUpdated = File.GetLastWriteTime(AlfaPath);
                }
            }
            catch (Exception) { set.Alfa = new string[0]; }
            return set;
        }

        /// <summary>Обновляет оба источника независимо друг от друга; файлы перезаписываются только при успехе.</summary>
        public static Task<CreditRatesUpdate> UpdateAsync(AppSettings settings)
        {
            string bankUrl = settings.CreditBankRatesUrl;
            string folder = Folder;
            return Task.Run(() =>
            {
                var result = new CreditRatesUpdate();
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2

                try
                {
                    string message;
                    result.NbrbOk = UpdateNbrb(folder, out message);
                    result.NbrbMessage = message;
                }
                catch (Exception ex) { result.NbrbMessage = "не удалось получить курсы НБ РБ: " + ex.Message; }

                try
                {
                    string message;
                    result.AlfaOk = UpdateBank(bankUrl, folder, out message);
                    result.AlfaMessage = message;
                }
                catch (Exception ex) { result.AlfaMessage = "не удалось получить курсы банка: " + ex.Message; }

                return result;
            });
        }

        // ── НБ РБ ────────────────────────────────────────────────────────────────────────────

        private static bool UpdateNbrb(string folder, out string message)
        {
            Directory.CreateDirectory(folder);
            var serializer = new DataContractJsonSerializer(typeof(NbrbRate));
            var rates = new List<NbrbRate>();
            foreach (string url in NbrbUrls)
            {
                string json = Download(url);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var rate = (NbrbRate)serializer.ReadObject(stream);
                    if (rate == null || string.IsNullOrEmpty(rate.Abbreviation) || rate.OfficialRate <= 0)
                    {
                        message = "ответ НБ РБ без курса (" + url + ")";
                        return false;
                    }
                    rates.Add(rate);
                }
            }

            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(List<NbrbRate>)).WriteObject(stream, rates);
                File.WriteAllBytes(Path.Combine(folder, NbrbFile), stream.ToArray());
            }
            message = "курсы НБ РБ обновлены";
            return true;
        }

        // ── Банк ─────────────────────────────────────────────────────────────────────────────

        private static bool UpdateBank(string url, string folder, out string message)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                message = "в настройках не задан адрес сайта с курсами банка";
                return false;
            }

            string html = Download(url);
            string[] lines;
            if (!TryParseBankTable(html, out lines, out message)) return false;

            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, AlfaFile), lines);
            message = "курсы банка обновлены";
            return true;
        }

        /// <summary>
        /// Текст таблицы с id «tblRatesH3» (без тегов) → три курса фиксированной длины: сначала отбрасываются первые 97 символов,
        /// затем берутся по 6 символов со смещений 6, 18 и 30 (USD, EUR, RUB) — так же, как в исходной программе.
        /// </summary>
        public static bool TryParseBankTable(string html, out string[] lines, out string error)
        {
            lines = null;
            string text = ExtractInnerText(html, BankTableId);
            if (text == null)
            {
                error = "на сайте не найдена таблица курсов (id «" + BankTableId + "»)";
                return false;
            }

            int need = BankTextSkip + BankRateOffsets[BankRateOffsets.Length - 1] + BankRateLength;
            if (text.Length < need)
            {
                error = "таблица курсов банка короче ожидаемой (" + text.Length + " симв., нужно не меньше " + need + ")";
                return false;
            }

            string table = text.Substring(BankTextSkip);
            var parsed = new string[BankRateOffsets.Length];
            string[] names = { "USD", "EUR", "RUB" };
            for (int i = 0; i < parsed.Length; i++)
            {
                string value = table.Substring(BankRateOffsets[i], BankRateLength);
                double number;
                if (!double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) || number <= 0)
                {
                    error = "в таблице курсов банка вместо курса " + names[i] + " найдено «" + value.Trim() + "» — формат страницы изменился";
                    return false;
                }
                parsed[i] = value.Replace(',', '.');
            }

            lines = parsed;
            error = null;
            return true;
        }

        /// <summary>Содержимое элемента с заданным id без тегов (аналог HtmlNode.InnerText); null, если элемента нет.</summary>
        public static string ExtractInnerText(string html, string id)
        {
            if (string.IsNullOrEmpty(html)) return null;

            Match open = Regex.Match(html, "<([a-zA-Z][a-zA-Z0-9]*)\\b[^>]*\\bid\\s*=\\s*[\"']" + Regex.Escape(id) + "[\"'][^>]*>", RegexOptions.IgnoreCase);
            if (!open.Success) return null;

            string tag = open.Groups[1].Value;
            var tags = new Regex("<(/?)" + Regex.Escape(tag) + "\\b[^>]*>", RegexOptions.IgnoreCase);
            int depth = 1, end = -1;
            foreach (Match m in tags.Matches(html.Substring(open.Index + open.Length)))
            {
                depth += m.Groups[1].Value.Length == 0 ? 1 : -1;
                if (depth == 0) { end = open.Index + open.Length + m.Index; break; }
            }
            string inner = end < 0 ? html.Substring(open.Index + open.Length) : html.Substring(open.Index + open.Length, end - (open.Index + open.Length));

            inner = Regex.Replace(inner, "<!--.*?-->", "", RegexOptions.Singleline);
            return Regex.Replace(inner, "<[^>]*>", "");
        }

        private static string Download(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            request.UserAgent = "MultiTool";
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }
}
