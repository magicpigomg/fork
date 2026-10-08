using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using MultiTool.Models;

namespace MultiTool.Services
{
    /// <summary>
    /// Макрос .mac из значений платежа: код страны (из BIC), счёт получателя без пробелов,
    /// наименование получателя (не длиннее 140 символов) и BIC банка получателя (8 символов + «XXX»).
    /// Значения берутся с сайта, а чего там не хватает — из документа.
    /// </summary>
    public static class PaymentMacro
    {
        public static bool TryBuild(PaymentFields site, PaymentFields doc, AppSettings s, out string macro, out string error)
        {
            macro = "";
            string swift = Pick(site != null ? site.Swift : null, doc != null ? doc.Swift : null);
            string account = Pick(site != null ? site.RecipientAccount : null, doc != null ? doc.RecipientAccount : null);
            string name = Pick(site != null ? site.Recipient : null, doc != null ? doc.Recipient : null);

            if (site == null && doc == null)
            {
                error = "Сначала выберите значения из сайта и/или документа";
                return false;
            }

            string country = null;
            int start = Math.Max(0, s.PaymentCountryStart), length = Math.Max(0, s.PaymentCountryLength);
            if (swift != null && swift.Length >= start + length && length > 0)
                country = swift.Substring(start, length).ToUpperInvariant();

            var missing = new System.Collections.Generic.List<string>();
            if (country == null) missing.Add("код страны (нужен SWIFT/BIC банка получателя)");
            if (string.IsNullOrEmpty(account)) missing.Add("счёт получателя");
            if (string.IsNullOrEmpty(name)) missing.Add("наименование получателя");
            if (string.IsNullOrEmpty(swift)) missing.Add("BIC банка получателя");
            if (missing.Count > 0)
            {
                error = "Не хватает: " + string.Join(", ", missing);
                return false;
            }

            // Поле «Наименование получателя» ограничено по длине.
            int maxName = Math.Max(1, s.PaymentNameMaxLength);
            if (name.Length > maxName) name = name.Substring(0, maxName).TrimEnd();

            // BIC из 8 символов дополняем до 11 («XXX»).
            string bic = swift.Length == 8 ? swift + s.PaymentBicPad : swift;

            string text = s.PaymentMacroTemplate
                .Replace("{Country}", country)
                .Replace("{RecipientAccount}", Regex.Replace(account, @"\s+", ""))
                .Replace("{RecipientName}", name)
                .Replace("{Bic}", bic);

            macro = Regex.Replace(text, @"\r\n|\r|\n", "\r\n");
            error = "";
            return true;
        }

        /// <summary>Формирует макрос и сохраняет его (окно «Сохранить как» или постоянный файл — по настройкам).</summary>
        public static AcceptOutcome Create(PaymentFields site, PaymentFields doc, AppSettings s, Window owner)
        {
            string macro, error;
            if (!TryBuild(site, doc, s, out macro, out error))
                return new AcceptOutcome(AcceptStatus.Failed, error);

            string path;
            if (s.PaymentMacroAskPath)
            {
                path = AskPath(s, owner);
                if (path == null) return new AcceptOutcome(AcceptStatus.Cancelled, "Сохранение макроса отменено");
            }
            else
            {
                path = s.PaymentMacroSavePath;
                if (string.IsNullOrWhiteSpace(path))
                    return new AcceptOutcome(AcceptStatus.Failed, "Не задан путь сохранения макроса: укажите его в настройках или включите запрос пути");
            }

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                Encoding encoding = FileGenerator.GetEncoding(s);
                File.WriteAllText(path, macro, encoding);

                // Файл пишется в ANSI: символы вне кодовой страницы (например, турецкие буквы) заменятся на «?».
                bool lossy = encoding.GetString(encoding.GetBytes(macro)) != macro;
                string note = lossy ? " Внимание: часть символов не поддерживается ANSI и заменена на «?»." : "";
                return new AcceptOutcome(AcceptStatus.Saved, "Макрос сохранён: " + path + "." + note, "", path);
            }
            catch (Exception ex)
            {
                return new AcceptOutcome(AcceptStatus.Failed, "Не удалось сохранить файл: " + ex.Message);
            }
        }

        private static string AskPath(AppSettings s, Window owner)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Макросы (*.mac)|*.mac",
                DefaultExt = ".mac",
                FileName = "!recipient",
                Title = "Сохранить макрос .mac",
                OverwritePrompt = false
            };
            if (Directory.Exists(s.PaymentMacroFolder)) dialog.InitialDirectory = s.PaymentMacroFolder;

            bool? ok = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            return ok == true ? dialog.FileName : null;
        }

        private static string Pick(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : (!string.IsNullOrWhiteSpace(second) ? second : null);
        }
    }
}
