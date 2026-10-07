using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace MultiTool.Models
{
    /// <summary>Все настройки приложения. Хранятся в %AppData%\MultiTool\settings.xml.</summary>
    public sealed class AppSettings
    {
        private static AppSettings _current = new AppSettings();

        /// <summary>Текущие настройки; всегда обращайтесь через это свойство.</summary>
        [XmlIgnore]
        public static AppSettings Current
        {
            get { return _current; }
            set { _current = value; }
        }

        // ── Горячие клавиши ─────────────────────────────────────────────────────
        public bool HotkeysEnabled { get; set; } = true;
        public bool ShowToasts { get; set; } = true;
        public int MenuDelayMs { get; set; } = 200;          // пауза после ПКМ, пока откроется меню
        public int StepDelayMs { get; set; } = 100;          // пауза между нажатиями клавиш
        public int F7DelayMs { get; set; } = 300;            // пауза после F7
        public int ClipboardTimeoutMs { get; set; } = 2000;  // сколько ждать появления данных в буфере

        // ── Блокировка окон ─────────────────────────────────────────────────────
        public string BlockedWindows { get; set; } = "Сеанс A\nСеанс B\nСеанс C\nСеанс D";
        public bool PassKeyThroughWhenBlocked { get; set; } = true;

        // ── Тексты для Shift+F1..F3 ─────────────────────────────────────────────
        public string ShiftText1 { get; set; } = "Платежное поручение исполнено";
        public string ShiftText2 { get; set; } = "Уточните сумму приостановления (должна быть равна сумме остатка брони, либо сумме платежных поручений, необходимых для проведения)";
        public string ShiftText3 { get; set; } = "Документ помещён в ОХ";

        // ── Правила разбора буфера (F1) ─────────────────────────────────────────
        /// <summary>Для правил с №: сколько символов берём после №.</summary>
        public int TakeAfterMarker { get; set; } = 28;

        // «Отзыв документа»: после метки ищется префикс (BY), от его начала пропускаем N символов и берём M.
        public string RevocationKeyword { get; set; } = "Отзыв документа";
        public string PayerAccountLabel { get; set; } = "Номер счета плательщика";
        public string IbanPrefix { get; set; } = "BY";
        public int IbanSkip { get; set; } = 8;
        public int IbanLength { get; set; } = 13;

        // «Заявление» + «Номер приостанавливаемого распоряжения»
        public string StatementKeyword { get; set; } = "Заявление";
        public string SuspendedKeyword { get; set; } = "Номер приостанавливаемого распоряжения";
        public int SuspendedOccurrence { get; set; } = 2;
        public int SuspendedSkip { get; set; } = 8;
        public int SuspendedLength { get; set; } = 13;

        // Только «Заявление»
        public int StatementOccurrence { get; set; } = 1;
        public int StatementSkip { get; set; } = 9;
        public int StatementLength { get; set; } = 13;

        // «Распоряжение» (проверяется последним)
        public string OrderKeyword { get; set; } = "Распоряжение";
        public int OrderOccurrence { get; set; } = 2;
        public int OrderSkip { get; set; } = 9;
        public int OrderLength { get; set; } = 13;

        // ── Файлы приостановления ───────────────────────────────────────────────
        public string DatePath { get; set; } = DefaultFile("date.txt");
        public string PriostanovleniePath { get; set; } = DefaultFile("priostanovlenie.txt");
        public string DateFormat { get; set; } = "ddMMyy";
        public bool UseAnsiEncoding { get; set; } = true;
        public string DateFileTemplate { get; set; } = DefaultTemplates.DateFile;
        public string PriostanovlenieTemplate { get; set; } = DefaultTemplates.PriostanovlenieScript;

        // ── Accept: документ берётся из буфера обмена, номера строк считаются с 0 ──────
        public string AcceptTitlePrefix { get; set; } = "ОТЗЫВ ";   // копируется в буфер: префикс + строка заголовка
        public int AcceptTitleLine { get; set; } = 9;

        public int AcceptPayerLine { get; set; } = 19;              // счёт клиента
        public int AcceptPayerSkip { get; set; } = 33;
        public int AcceptPayerLength { get; set; } = 13;

        public int AcceptDateAcceptLine { get; set; } = 27;         // дата акцепта (до конца строки)
        public int AcceptDateAcceptSkip { get; set; } = 15;

        public int AcceptContractLine { get; set; } = 26;           // номер договора
        public int AcceptContractSkip { get; set; } = 16;

        public int AcceptYnpLine { get; set; } = 25;                // УНП бенефициара
        public int AcceptYnpSkip { get; set; } = 16;

        public int AcceptDateDocLine { get; set; } = 15;            // дата договора
        public int AcceptDateDocSkip { get; set; } = 6;

        public int AcceptNumberLine { get; set; } = 16;             // номер акцепта (с обрезкой пробелов)
        public int AcceptNumberSkip { get; set; } = 6;

        public string AcceptInputDateFormat { get; set; } = "dd.MM.yyyy";
        public string AcceptOutputDateFormat { get; set; } = "ddMMyy";
        public string AcceptFolder { get; set; } = "";              // папка по умолчанию в окне сохранения
        public string AcceptTemplate { get; set; } = DefaultTemplates.AcceptMacro;

        // ── Разбор номера (инструменты) ─────────────────────────────────────────
        public int NumberSkipFirst { get; set; } = 8;   // убрать первые N символов
        public int NumberTake { get; set; } = 13;       // оставить M символов
        public int NumberSkipSecond { get; set; } = 4;  // из результата убрать первые N
        public int NumberTakeThird { get; set; } = 6;   // из второго результата оставить M

        [XmlIgnore]
        public IReadOnlyList<string> BlockedWindowList
        {
            get
            {
                var result = new List<string>();
                foreach (string line in BlockedWindows.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) result.Add(trimmed);
                }
                return result;
            }
        }

        private static string DefaultFile(string name)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MultiTool", name);
        }
    }
}
