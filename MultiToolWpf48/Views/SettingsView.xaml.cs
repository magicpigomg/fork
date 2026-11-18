using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool.Views
{
    /// <summary>Страница настроек. Поля строятся из описаний и привязаны к AppSettings.Current.</summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = AppSettings.Current;
            Build();
        }

        private void Build()
        {
            Stack.Children.Clear();

            // ── Горячие клавиши ─────────────────────────────────────────────────────
            var hotkeys = AddCard("Горячие клавиши", "Паузы подбираются под скорость вашего приложения: если Tab-ы «пролетают» мимо полей, увеличьте паузы.");
            var delays = NewWrap();
            delays.Children.Add(Number("Пауза после ПКМ, мс", nameof(AppSettings.MenuDelayMs)));
            delays.Children.Add(Number("Пауза между клавишами, мс", nameof(AppSettings.StepDelayMs)));
            delays.Children.Add(Number("Пауза после F7, мс", nameof(AppSettings.F7DelayMs)));
            delays.Children.Add(Number("Ожидание буфера, мс", nameof(AppSettings.ClipboardTimeoutMs)));
            hotkeys.Children.Add(delays);
            hotkeys.Children.Add(Switch("Показывать всплывающие уведомления в углу экрана", nameof(AppSettings.ShowToasts)));

            // ── Блокировка окон ─────────────────────────────────────────────────────
            var blocked = AddCard("Блокировка окон", "Если заголовок активного окна начинается с одной из строк, сценарии не выполняются. По одному названию на строку.");
            blocked.Children.Add(Multiline("Заголовки окон", nameof(AppSettings.BlockedWindows), 96, mono: false));
            blocked.Children.Add(Switch("В таких окнах клавиши F1–F3 работают как обычные", nameof(AppSettings.PassKeyThroughWhenBlocked)));

            // ── Тексты ──────────────────────────────────────────────────────────────
            var texts = AddCard("Тексты для Shift+F1–F3", "Вводятся Unicode-символами, буфер обмена не затрагивается.");
            texts.Children.Add(Multiline("Shift+F1", nameof(AppSettings.ShiftText1), 62, mono: false));
            texts.Children.Add(Multiline("Shift+F2", nameof(AppSettings.ShiftText2), 86, mono: false));
            texts.Children.Add(Multiline("Shift+F3", nameof(AppSettings.ShiftText3), 62, mono: false));

            // ── Правила буфера ──────────────────────────────────────────────────────
            var rules = AddCard("Правила разбора буфера (F1)",
                "Тип документа определяется по ключевым словам в скопированном тексте. Проверка идёт сверху вниз: платёжное поручение → отзыв → заявление → распоряжение. " +
                "Регистр, лишние пробелы и «е/ё» не важны.");
            rules.Children.Add(Row(
                Number("Символов после № (для всех №)", nameof(AppSettings.TakeAfterMarker)),
                Number("Три №: брать по счёту (0 — выкл.)", nameof(AppSettings.OccurrenceWhenThreeMarkers))));
            var rBooking = NewWrap();
            rBooking.Children.Add(Text("Три №, но берётся второй, если в тексте есть фраза", nameof(AppSettings.BookingOrderKeyword), 560));
            rules.Children.Add(rBooking);

            // ── Отзывы ──────────────────────────────────────────────────────────
            var revocations = AddBlock(rules, "rules-revocation", "Отзывы",
                "Отзыв документа и заявление на отзыв — счёт плательщика (BY…)");
            var r1 = NewWrap();
            r1.Children.Add(Text("Ключевая фраза «Отзыв»", nameof(AppSettings.RevocationKeyword), 260));
            r1.Children.Add(Text("Ключевая фраза «Заявление на отзыв»", nameof(AppSettings.RevocationRequestKeyword), 330));
            r1.Children.Add(Text("Метка перед счётом", nameof(AppSettings.PayerAccountLabel), 260));
            r1.Children.Add(Text("Префикс", nameof(AppSettings.IbanPrefix), 100));
            r1.Children.Add(Number("Пропустить", nameof(AppSettings.IbanSkip)));
            r1.Children.Add(Number("Взять", nameof(AppSettings.IbanLength)));
            r1.Children.Add(Number("Весь счёт: символов", nameof(AppSettings.IbanFullLength)));
            revocations.Children.Add(r1);
            revocations.Children.Add(Switch("Заявление на отзыв: выделять весь счёт (с BY) — включено весь номер, выключено часть после ALFA",
                nameof(AppSettings.RevocationRequestWholeAccount)));

            // ── Приостановления ─────────────────────────────────────────────────
            var suspensions = AddBlock(rules, "rules-suspension", "Приостановления",
                "Заявления: с приостанавливаемым распоряжением, обычные и свои правила");
            suspensions.Children.Add(BlockTitle("Заявление с приостанавливаемым распоряжением"));
            var r2 = NewWrap();
            r2.Children.Add(Text("Ключевая фраза «Заявление»", nameof(AppSettings.StatementKeyword), 260));
            r2.Children.Add(Text("Доп. фраза", nameof(AppSettings.SuspendedKeyword), 330));
            r2.Children.Add(Text("Или доп. фраза", nameof(AppSettings.SuspendedAltKeyword), 330));
            r2.Children.Add(Number("Какой по счёту №", nameof(AppSettings.SuspendedOccurrence)));
            r2.Children.Add(Number("Пропустить", nameof(AppSettings.SuspendedSkip)));
            r2.Children.Add(Number("Взять", nameof(AppSettings.SuspendedLength)));
            suspensions.Children.Add(r2);

            suspensions.Children.Add(Subtitle("Только заявление"));
            var r3 = NewWrap();
            r3.Children.Add(Number("Какой по счёту №", nameof(AppSettings.StatementOccurrence)));
            r3.Children.Add(Number("Пропустить", nameof(AppSettings.StatementSkip)));
            r3.Children.Add(Number("Взять", nameof(AppSettings.StatementLength)));
            suspensions.Children.Add(r3);

            suspensions.Children.Add(Subtitle("Свои правила для заявлений"));
            suspensions.Children.Add(new TextBlock
            {
                Text = "Нужно распознать новый вид заявления — добавьте правило кнопкой «+». Правила проверяются сверху вниз раньше встроенных " +
                       "(срабатывает первое подходящее) и только для текстов со словом «Заявление». Ключевая фраза обязательна; " +
                       "доп. фразу заполняйте, если одной фразы мало: тогда в тексте должны быть обе.",
                Style = (Style)FindResource("Muted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            var ruleList = new StackPanel();
            foreach (StatementRule rule in AppSettings.Current.StatementRules)
                ruleList.Children.Add(BuildRuleCard(ruleList, rule));
            suspensions.Children.Add(ruleList);
            var addRule = new Button
            {
                Content = "+  Добавить правило",
                Style = (Style)FindResource("GhostButton"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 0)
            };
            addRule.Click += delegate
            {
                var rule = new StatementRule();
                AppSettings.Current.StatementRules.Add(rule);
                ruleList.Children.Add(BuildRuleCard(ruleList, rule));
            };
            suspensions.Children.Add(addRule);

            // ── Распоряжения ────────────────────────────────────────────────────
            var orders = AddBlock(rules, "rules-order", "Распоряжения", "Проверяется последним, чтобы не менять поведение заявлений");
            var r4 = NewWrap();
            r4.Children.Add(Text("Ключевая фраза", nameof(AppSettings.OrderKeyword), 260));
            r4.Children.Add(Number("Какой по счёту №", nameof(AppSettings.OrderOccurrence)));
            r4.Children.Add(Number("Пропустить", nameof(AppSettings.OrderSkip)));
            r4.Children.Add(Number("Взять", nameof(AppSettings.OrderLength)));
            orders.Children.Add(r4);

            // ── Платёжные поручения ─────────────────────────────────────────────
            var payments = AddBlock(rules, "rules-payment", "Платёжные поручения",
                "Платёжное поручение, прошедшее предварительный контроль, — код «ABOWD…» (проверяется первым)");
            var r5 = NewWrap();
            r5.Children.Add(Text("Ключевая фраза", nameof(AppSettings.PaymentOrderKeyword), 260));
            r5.Children.Add(Text("Признак", nameof(AppSettings.ControlCodeKeyword), 330));
            r5.Children.Add(Text("Префикс кода", nameof(AppSettings.ControlCodePrefix), 140));
            r5.Children.Add(Number("Общая длина кода", nameof(AppSettings.ControlCodeLength)));
            payments.Children.Add(r5);

            // ── Файлы ───────────────────────────────────────────────────────────────
            var files = AddCard("Файлы приостановления", "В шаблонах {Date} заменяется на сегодняшнюю дату в выбранном формате.");
            files.Children.Add(PathField("Файл с датой", nameof(AppSettings.DatePath)));
            files.Children.Add(PathField("Файл макроса приостановления", nameof(AppSettings.PriostanovleniePath)));
            var fileOpts = NewWrap();
            fileOpts.Children.Add(Text("Формат даты", nameof(AppSettings.DateFormat), 140));
            files.Children.Add(fileOpts);
            files.Children.Add(Switch("Кодировка ANSI (Windows-1251), как раньше; выключено — UTF-8", nameof(AppSettings.UseAnsiEncoding)));
            files.Children.Add(Multiline("Шаблон файла с датой", nameof(AppSettings.DateFileTemplate), 70, mono: true));
            files.Children.Add(Multiline("Шаблон макроса приостановления", nameof(AppSettings.PriostanovlenieTemplate), 320, mono: true));

            // ── Кредит ──────────────────────────────────────────────────────────────
            var credit = AddCard("Кредит",
                "Курсы для макросов !80 / !90 / !70 (вкладка «Кредит»). Файлы курсов rates_nbrb.json и rates_alfa.txt лежат в папке настроек.");
            var creditRow = NewWrap();
            creditRow.Children.Add(Text("Адрес страницы с курсами банка", nameof(AppSettings.CreditBankRatesUrl), 460));
            credit.Children.Add(creditRow);
            var creditFolder = NewWrap();
            creditFolder.Children.Add(Text("Папка курсов (пусто — предложить выбрать при открытии вкладки «Кредит»)", nameof(AppSettings.CreditRatesFolder), 460));
            credit.Children.Add(creditFolder);

            // ── Accept ──────────────────────────────────────────────────────────────
            var accept = AddCard("Accept",
                "Документ берётся из буфера обмена. Номера строк считаются с 0, «пропустить» — сколько символов отбросить от начала строки; " +
                "без поля «взять» значение идёт до конца строки.");
            accept.Children.Add(Subtitle("Строка для буфера обмена"));
            var a0 = NewWrap();
            a0.Children.Add(Text("Префикс", nameof(AppSettings.AcceptTitlePrefix), 140));
            a0.Children.Add(Number("Номер строки", nameof(AppSettings.AcceptTitleLine)));
            accept.Children.Add(a0);

            accept.Children.Add(Subtitle("Поля документа"));
            accept.Children.Add(Row(
                Number("Счёт клиента: строка", nameof(AppSettings.AcceptPayerLine)),
                Number("пропустить", nameof(AppSettings.AcceptPayerSkip)),
                Number("взять", nameof(AppSettings.AcceptPayerLength))));
            accept.Children.Add(Row(
                Number("Дата акцепта: строка", nameof(AppSettings.AcceptDateAcceptLine)),
                Number("пропустить", nameof(AppSettings.AcceptDateAcceptSkip))));
            accept.Children.Add(Row(
                Number("Номер договора: строка", nameof(AppSettings.AcceptContractLine)),
                Number("пропустить", nameof(AppSettings.AcceptContractSkip))));
            accept.Children.Add(Row(
                Number("УНП бенефициара: строка", nameof(AppSettings.AcceptYnpLine)),
                Number("пропустить", nameof(AppSettings.AcceptYnpSkip))));
            accept.Children.Add(Row(
                Number("Дата договора: строка", nameof(AppSettings.AcceptDateDocLine)),
                Number("пропустить", nameof(AppSettings.AcceptDateDocSkip))));
            accept.Children.Add(Row(
                Number("Номер акцепта: строка", nameof(AppSettings.AcceptNumberLine)),
                Number("пропустить", nameof(AppSettings.AcceptNumberSkip))));

            accept.Children.Add(Subtitle("Даты и сохранение"));
            var a2 = NewWrap();
            a2.Children.Add(Text("Формат даты в документе", nameof(AppSettings.AcceptInputDateFormat), 200));
            a2.Children.Add(Text("Формат даты в макросе", nameof(AppSettings.AcceptOutputDateFormat), 200));
            accept.Children.Add(a2);
            accept.Children.Add(Subtitle("Когда формируется Accept по F1"));
            accept.Children.Add(Row(Text("Признак (вместе с «Отзыв документа»)", nameof(AppSettings.AcceptKeyword), 330)));

            accept.Children.Add(Subtitle("Куда сохранять файл"));
            accept.Children.Add(Switch("Спрашивать путь при каждом сохранении (окно «Сохранить как»)", nameof(AppSettings.AcceptAskPath)));
            accept.Children.Add(PathField("Папка по умолчанию в окне «Сохранить как» (необязательно)", nameof(AppSettings.AcceptFolder)));
            accept.Children.Add(PathField("Постоянный файл — используется, когда запрос пути выключен", nameof(AppSettings.AcceptSavePath)));
            accept.Children.Add(Multiline(
                "Шаблон макроса !accept.mac (подстановки: {PayerAccount} {DateAccept} {NumberContract} {YnpBen} {DateDoc} {NumberAccept})",
                nameof(AppSettings.AcceptTemplate), 260, mono: true));

            // ── Макрос .mac из полей платежа ────────────────────────────────────────
            var macro = AddCard("Макрос .mac из полей платежа",
                "Создаётся на странице «Сравнение текстов» → «Поля платежа» (включите переключатель «Создавать файл .mac»). " +
                "В макрос попадают: код страны из BIC, счёт получателя без пробелов, наименование получателя и BIC банка получателя.");
            macro.Children.Add(Switch("Спрашивать путь при каждом сохранении (окно «Сохранить как»)", nameof(AppSettings.PaymentMacroAskPath)));
            macro.Children.Add(PathField("Папка по умолчанию в окне «Сохранить как» (необязательно)", nameof(AppSettings.PaymentMacroFolder)));
            macro.Children.Add(PathField("Постоянный файл — используется, когда запрос пути выключен", nameof(AppSettings.PaymentMacroSavePath)));
            macro.Children.Add(Row(
                Number("Длина поля наименования", nameof(AppSettings.PaymentNameMaxLength)),
                Number("Код страны: с символа (с 0)", nameof(AppSettings.PaymentCountryStart)),
                Number("Код страны: сколько символов", nameof(AppSettings.PaymentCountryLength))));
            macro.Children.Add(Row(Text("Добавка к BIC из 8 символов", nameof(AppSettings.PaymentBicPad), 200)));
            macro.Children.Add(Multiline(
                "Шаблон макроса (подстановки: {Country} {RecipientAccount} {RecipientName} {Bic})",
                nameof(AppSettings.PaymentMacroTemplate), 230, mono: true));

            // ── Разбор номера ───────────────────────────────────────────────────────
            var number = AddCard("Разбор номера (Инструменты)", "Результат 1 — без первых N символов, оставить M. Результат 2 — без первых K. Результат 3 — первые L символов.");
            var nw = NewWrap();
            nw.Children.Add(Number("Убрать первые (N)", nameof(AppSettings.NumberSkipFirst)));
            nw.Children.Add(Number("Оставить (M)", nameof(AppSettings.NumberTake)));
            nw.Children.Add(Number("Убрать первые (K)", nameof(AppSettings.NumberSkipSecond)));
            nw.Children.Add(Number("Первые (L)", nameof(AppSettings.NumberTakeThird)));
            number.Children.Add(nw);
        }

        // ── Кнопки ───────────────────────────────────────────────────────────────────────────

        private void OnSave(object sender, RoutedEventArgs e)
        {
            string error = SettingsStore.Save(AppSettings.Current);
            SaveStatus.Text = error == null ? "Сохранено" : "Ошибка: " + error;
            SaveStatus.Foreground = (Brush)FindResource(error == null ? "SuccessBrush" : "DangerBrush");
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show("Сбросить все настройки к значениям по умолчанию?", "MultiTool",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            bool hotkeys = AppSettings.Current.HotkeysEnabled;
            AppSettings.Current = new AppSettings { HotkeysEnabled = hotkeys };
            DataContext = AppSettings.Current;
            Build();
            SaveStatus.Text = "Значения по умолчанию восстановлены — нажмите «Сохранить»";
            SaveStatus.Foreground = (Brush)FindResource("MutedBrush");
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            Process.Start(new ProcessStartInfo(SettingsStore.Folder) { UseShellExecute = true });
        }

        // ── Построение полей ──────────────────────────────────────────────────────────────────

        private StackPanel AddCard(string title, string subtitle)
        {
            var inner = new StackPanel();
            inner.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
            inner.Children.Add(new TextBlock
            {
                Text = subtitle,
                Style = (Style)FindResource("Muted"),
                Margin = new Thickness(0, 4, 0, 16)
            });

            var card = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 0, 0, 16), Child = inner };
            Stack.Children.Add(card);
            return inner;
        }

        // ── Сворачиваемые блоки ──────────────────────────────────────────────────────────────

        // Открыт блок или свёрнут — помним, пока приложение работает (страница настроек пересоздаётся при сбросе).
        private static readonly Dictionary<string, bool> BlockState = new Dictionary<string, bool>();

        /// <summary>Блок внутри карточки: заголовок со стрелкой, по клику на заголовок (или стрелку) содержимое сворачивается и разворачивается.</summary>
        private StackPanel AddBlock(Panel parent, string key, string title, string hint)
        {
            var content = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            var arrow = new TextBlock
            {
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 14,
                Foreground = (Brush)FindResource("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            var titles = new StackPanel();
            titles.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
            titles.Children.Add(new TextBlock
            {
                Text = hint,
                Style = (Style)FindResource("Muted"),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var header = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(titles);
            Grid.SetColumn(arrow, 1);
            header.Children.Add(arrow);

            bool expanded;
            BlockState.TryGetValue(key, out expanded);
            Action apply = delegate
            {
                content.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                arrow.Text = expanded ? "\uE70E" : "\uE70D";   // стрелка вверх — блок открыт, вниз — свёрнут
            };
            header.MouseLeftButtonUp += delegate
            {
                expanded = !expanded;
                BlockState[key] = expanded;
                apply();
            };
            apply();

            var inner = new StackPanel();
            inner.Children.Add(header);
            inner.Children.Add(content);
            parent.Children.Add(new Border
            {
                Background = (Brush)FindResource("CardHoverBrush"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 12),
                Child = inner
            });
            return content;
        }

        /// <summary>Первый подзаголовок внутри блока (без большого отступа сверху).</summary>
        private TextBlock BlockTitle(string text)
        {
            return new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
        }

        // ── Редактор своих правил заявлений ──────────────────────────────────────────────────

        /// <summary>Карточка одного правила; поля привязаны к самому правилу (DataContext), «Удалить» убирает его из списка и из настроек.</summary>
        private FrameworkElement BuildRuleCard(Panel list, StatementRule rule)
        {
            var inner = new StackPanel { DataContext = rule };

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var remove = new Button { Content = "Удалить", Style = (Style)FindResource("DangerButton"), Padding = new Thickness(12, 6, 12, 6) };
            DockPanel.SetDock(remove, Dock.Right);
            top.Children.Add(remove);
            top.Children.Add(Switch("Правило включено", nameof(StatementRule.Enabled)));
            inner.Children.Add(top);

            var fields = NewWrap();
            fields.Children.Add(Text("Название (для журнала, необязательно)", nameof(StatementRule.Name), 260));
            fields.Children.Add(Text("Ключевая фраза", nameof(StatementRule.Keyword), 330));
            fields.Children.Add(Text("Доп. фраза (если нужна)", nameof(StatementRule.AltKeyword), 330));
            fields.Children.Add(Number("Какой по счёту №", nameof(StatementRule.Occurrence)));
            fields.Children.Add(Number("Пропустить", nameof(StatementRule.Skip)));
            fields.Children.Add(Number("Взять", nameof(StatementRule.Length)));
            inner.Children.Add(fields);
            inner.Children.Add(Switch("Три №: брать по счёту из общих настроек (если № три и больше, а правило берёт второй)",
                nameof(StatementRule.ThreeMarkersRule)));

            var card = new Border
            {
                Background = (Brush)FindResource("CardBrush"),   // темнее блока, светлее полей ввода — поля видны
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 0),
                Margin = new Thickness(0, 0, 0, 10),
                Child = inner
            };

            remove.Click += delegate
            {
                string name = string.IsNullOrWhiteSpace(rule.Name) ? rule.Keyword : rule.Name;
                var answer = MessageBox.Show("Удалить правило" + (string.IsNullOrWhiteSpace(name) ? "" : " «" + name.Trim() + "»") + "?", "MultiTool",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;
                AppSettings.Current.StatementRules.Remove(rule);
                list.Children.Remove(card);
            };
            return card;
        }

        private static WrapPanel NewWrap()
        {
            return new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        }

        private static WrapPanel Row(params FrameworkElement[] fields)
        {
            var row = NewWrap();
            foreach (var field in fields) row.Children.Add(field);
            return row;
        }

        private TextBlock Subtitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 18, 0, 10)
            };
        }

        private FrameworkElement Labeled(string label, FrameworkElement input, double width, Thickness margin)
        {
            var panel = new StackPanel { Width = width, Margin = margin };
            panel.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("FieldLabel") });
            panel.Children.Add(input);
            return panel;
        }

        private FrameworkElement Number(string label, string property)
        {
            var box = new TextBox { Style = (Style)FindResource("MonoTextBox") };
            box.SetBinding(TextBox.TextProperty, TwoWay(property));
            return Labeled(label, box, 200, new Thickness(0, 0, 14, 14));
        }

        private FrameworkElement Text(string label, string property, double width)
        {
            var box = new TextBox();
            box.SetBinding(TextBox.TextProperty, TwoWay(property));
            return Labeled(label, box, width, new Thickness(0, 0, 14, 14));
        }

        private FrameworkElement Multiline(string label, string property, double height, bool mono)
        {
            var box = new TextBox
            {
                Style = (Style)FindResource(mono ? "MonoTextBox" : "BaseTextBox"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top,
                Height = height
            };
            box.SetBinding(TextBox.TextProperty, TwoWay(property));
            return Labeled(label, box, double.NaN, new Thickness(0, 0, 0, 14));
        }

        private FrameworkElement Switch(string label, string property)
        {
            var toggle = new CheckBox
            {
                Content = label,
                Style = (Style)FindResource("SwitchStyle"),
                Margin = new Thickness(0, 4, 0, 14)
            };
            toggle.SetBinding(CheckBox.IsCheckedProperty, TwoWay(property));
            return toggle;
        }

        private FrameworkElement PathField(string label, string property)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = new TextBox();
            box.SetBinding(TextBox.TextProperty, TwoWay(property));
            var browse = new Button { Content = "Обзор…", Margin = new Thickness(8, 0, 0, 0) };
            browse.Click += delegate
            {
                var dialog = new SaveFileDialog
                {
                    FileName = Path.GetFileName(box.Text),
                    InitialDirectory = Directory.Exists(Path.GetDirectoryName(box.Text)) ? Path.GetDirectoryName(box.Text) : null,
                    OverwritePrompt = false,
                    Filter = "Все файлы|*.*"
                };
                if (dialog.ShowDialog() == true) box.Text = dialog.FileName;
            };
            Grid.SetColumn(browse, 1);
            grid.Children.Add(box);
            grid.Children.Add(browse);

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("FieldLabel") });
            panel.Children.Add(grid);
            return panel;
        }

        private static Binding TwoWay(string property)
        {
            return new Binding(property)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }
    }
}
