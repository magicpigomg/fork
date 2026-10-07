using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

// Добавьте этот файл в проект (Проект -> Добавить существующий элемент).
// Form1 уже partial, поэтому ваш Form1.cs и дизайнер менять не нужно.
namespace WindowsFormsApp1
{
    public partial class Form1
    {
        private const int F1HotkeyId = 0xF1;

        private const int MenuDelayMs = 200;  // пауза после ПКМ, пока откроется меню
        private const int StepDelayMs = 100;  // пауза между нажатиями клавиш
        private const int ClipboardTimeoutMs = 2000;

        private const int F2HotkeyId = 0xF2;
        private const int F3HotkeyId = 0xF3;
        private const int ShiftF1HotkeyId = 0xF4;
        private const int ShiftF3HotkeyId = 0xF6;
        private const int F7DelayMs = 300;    // пауза после F7, пока откроется окно/режим

        private bool _f1Busy;

        // Номера горячих клавиш: F1..F3 = 0xF1..0xF3, Shift+F1..F3 = 0xF4..0xF6.
        private static bool IsOurHotkey(int id)
        {
            return id >= F1HotkeyId && id <= ShiftF3HotkeyId;
        }

        private static int FKeyNumber(int id)
        {
            return (id - F1HotkeyId) % 3 + 1; // 1..3
        }

        private static uint HotkeyVk(int id)
        {
            return (uint)(0x70 + FKeyNumber(id) - 1); // VK_F1 = 0x70
        }

        private static uint HotkeyMods(int id)
        {
            return id >= ShiftF1HotkeyId ? F1Native.MOD_SHIFT | F1Native.MOD_NOREPEAT : F1Native.MOD_NOREPEAT;
        }

        private static string HotkeyName(int id)
        {
            return (id >= ShiftF1HotkeyId ? "Shift+" : "") + "F" + FKeyNumber(id);
        }

        // Тексты для Shift+F1, Shift+F2, Shift+F3.
        private static readonly string[] ShiftTexts =
        {
            "Платежное поручение исполнено",
            "Уточните сумму приостановления (должна быть равна сумме остатка брони, либо сумме платежных поручений, необходимых для проведения)",
            "Документ помещён в ОХ"
        };

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            for (int id = F1HotkeyId; id <= ShiftF3HotkeyId; id++)
            {
                if (!F1Native.RegisterHotKey(Handle, id, HotkeyMods(id), HotkeyVk(id)))
                    ShowF1Status("Не удалось зарегистрировать " + HotkeyName(id) + " (занята другой программой)");
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            for (int id = F1HotkeyId; id <= ShiftF3HotkeyId; id++)
                F1Native.UnregisterHotKey(Handle, id);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == F1Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (IsOurHotkey(id))
                {
                    if (IsBlockedWindowActive())
                    {
                        var ignoredPass = PassKeyThroughAsync(id);
                        return;
                    }
                    if (id == F1HotkeyId) { var ignored = RunF1ScenarioAsync(); return; }
                    if (id == F2HotkeyId) { var ignored = RunF2ScenarioAsync(); return; }
                    if (id == F3HotkeyId) { var ignored = RunF3ScenarioAsync(); return; }
                    var ignoredText = TypeTextAsync(ShiftTexts[FKeyNumber(id) - 1], HotkeyName(id));
                    return;
                }
            }
            base.WndProc(ref m);
        }

        // Если активно одно из этих окон — сценарии не выполняются.
        // Заголовок окна должен начинаться с указанного текста (регистр не важен).
        private static readonly string[] BlockedWindowTitles = { "Сеанс A", "Сеанс B", "Сеанс C", "Сеанс D" };

        // true — в заблокированном окне F1/F2/F3 и Shift+F1/F2/F3 работают как обычные клавиши;
        // false — нажатие просто игнорируется.
        private static readonly bool PassKeyThroughWhenBlocked = true;

        private static bool IsBlockedWindowActive()
        {
            string title = F1Native.GetActiveWindowTitle();
            foreach (string blocked in BlockedWindowTitles)
            {
                if (title.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // RegisterHotKey «съедает» клавишу, поэтому в заблокированном окне отправляем её заново:
        // на мгновение снимаем горячую клавишу, нажимаем F-клавишу и регистрируем снова.
        // Для Shift+F-клавиш Shift физически зажат, поэтому окно получит именно Shift+F.
        private async Task PassKeyThroughAsync(int id)
        {
            if (!PassKeyThroughWhenBlocked) return;

            F1Native.UnregisterHotKey(Handle, id);
            try
            {
                F1Keys.Press((ushort)HotkeyVk(id), false);
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                ShowF1Status(ex.Message);
            }
            finally
            {
                F1Native.RegisterHotKey(Handle, id, HotkeyMods(id), HotkeyVk(id));
            }
        }

        // Shift+F1/F2/F3: набирает готовый текст. Буфер обмена не используется (его не затираем).
        private async Task TypeTextAsync(string text, string name)
        {
            if (_f1Busy) return;
            _f1Busy = true;
            try
            {
                // Ждём, пока пользователь отпустит Shift, иначе символы придут с зажатым Shift.
                for (int i = 0; i < 30 && F1Native.IsShiftDown(); i++)
                    await Task.Delay(50);

                const int chunk = 8;
                for (int pos = 0; pos < text.Length; pos += chunk)
                {
                    F1Keys.TypeUnicode(text.Substring(pos, Math.Min(chunk, text.Length - pos)));
                    await Task.Delay(10);
                }

                ShowF1Status(name + ": текст введён");
            }
            catch (Exception ex)
            {
                ShowF1Status(ex.Message);
            }
            finally
            {
                _f1Busy = false;
            }
        }

        // F3: F7, Tab x4, Ctrl+V, Tab x8, стрелка влево, Tab x3, Enter.
        private async Task RunF3ScenarioAsync()
        {
            if (_f1Busy) return;
            _f1Busy = true;
            try
            {
                F1Keys.Press(F1Keys.VK_F7, false);
                await Task.Delay(F7DelayMs);

                await PressManyAsync(F1Keys.VK_TAB, false, 4);
                await F1Keys.CtrlV();
                await Task.Delay(StepDelayMs);

                await PressManyAsync(F1Keys.VK_TAB, false, 8);
                F1Keys.Press(F1Keys.VK_LEFT, true);
                await Task.Delay(StepDelayMs);

                await PressManyAsync(F1Keys.VK_TAB, false, 3);
                F1Keys.Press(F1Keys.VK_RETURN, false);

                ShowF1Status("F3: выполнено");
            }
            catch (Exception ex)
            {
                ShowF1Status(ex.Message);
            }
            finally
            {
                _f1Busy = false;
            }
        }

        // F2: F7, Tab x3, Backspace, Tab x8, стрелка вправо, Tab x3, Enter.
        private async Task RunF2ScenarioAsync()
        {
            if (_f1Busy) return;
            _f1Busy = true;
            try
            {
                F1Keys.Press(F1Keys.VK_F7, false);
                await Task.Delay(F7DelayMs);

                await PressManyAsync(F1Keys.VK_TAB, false, 3);
                F1Keys.Press(F1Keys.VK_BACK, false);
                await Task.Delay(StepDelayMs);

                await PressManyAsync(F1Keys.VK_TAB, false, 8);
                F1Keys.Press(F1Keys.VK_RIGHT, true);
                await Task.Delay(StepDelayMs);

                await PressManyAsync(F1Keys.VK_TAB, false, 3);
                F1Keys.Press(F1Keys.VK_RETURN, false);

                ShowF1Status("F2: выполнено");
            }
            catch (Exception ex)
            {
                ShowF1Status(ex.Message);
            }
            finally
            {
                _f1Busy = false;
            }
        }

        private static async Task PressManyAsync(ushort vk, bool extended, int count)
        {
            for (int i = 0; i < count; i++)
            {
                F1Keys.Press(vk, extended);
                await Task.Delay(StepDelayMs);
            }
        }

        private async Task RunF1ScenarioAsync()
        {
            if (_f1Busy) return;
            _f1Busy = true;
            try
            {
                uint seqBefore = F1Native.GetClipboardSequenceNumber();

                await F1Keys.CtrlA();
                await Task.Delay(StepDelayMs);
                F1Keys.RightClick();
                await Task.Delay(MenuDelayMs);
                F1Keys.Press(F1Keys.VK_DOWN, true);
                await Task.Delay(StepDelayMs);
                F1Keys.Press(F1Keys.VK_RIGHT, true);
                await Task.Delay(StepDelayMs);
                F1Keys.Press(F1Keys.VK_RETURN, false);

                // Ждём, пока пункт меню положит данные в буфер (если буфер не изменился — берём то, что есть).
                for (int waited = 0; waited < ClipboardTimeoutMs; waited += 50)
                {
                    if (F1Native.GetClipboardSequenceNumber() != seqBefore) break;
                    await Task.Delay(50);
                }
                await Task.Delay(50);

                ProcessF1Clipboard();
            }
            catch (Exception ex)
            {
                ShowF1Status(ex.Message);
            }
            finally
            {
                _f1Busy = false;
            }
        }

        private void ProcessF1Clipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) { ShowF1Status("В буфере нет текста"); return; }

                string result, kind, error;
                if (!F1ClipboardProcessor.TryExtract(Clipboard.GetText(), out result, out kind, out error))
                {
                    ShowF1Status(error);
                    return;
                }

                Clipboard.SetText(result);
                ShowF1Status(kind + ": " + result);
            }
            catch (Exception ex)
            {
                // Буфер может быть временно занят другим процессом.
                ShowF1Status(ex.Message);
            }
        }

        // Статус пишется в заголовок окна. Если у Form1 есть Label — замените тело на label.Text = text;
        private void ShowF1Status(string text)
        {
            Text = text;
        }
    }

    internal static class F1ClipboardProcessor
    {
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Признаки типа документа (пробелы/переносы между словами не важны).
        private static readonly Regex RevocationRx = new Regex(@"Отзыв\s+документа", Opt);
        private static readonly Regex StatementRx = new Regex(@"Заявление", Opt);
        private static readonly Regex SuspendedRx = new Regex(@"Номер\s+приостанавливаемого\s+распоряжения", Opt);
        private static readonly Regex PayerAccountRx = new Regex(@"Номер\s+сч[её]та\s+плательщика", Opt);

        // --- Настройки правил: меняйте здесь ---------------------------------------

        // Общие для правил с №: после № берём 28 символов, из них пропускаем N и берём M.
        private const char Marker = '№';
        private const int TakeAfterMarker = 28;

        // «Заявление» + «Номер приостанавливаемого распоряжения»: второй №, пропускаем 8, берём 13 знаков.
        private const int SuspendedOccurrence = 2;
        private const int SuspendedSkip = 8;
        private const int SuspendedLength = 13;

        // Только «Заявление»: первый №, пропускаем 9, берём 13 знаков.
        private const int StatementOccurrence = 1;
        private const int StatementSkip = 9;
        private const int StatementLength = 13;

        // «Отзыв документа»: после «Номер счета плательщика» находим «BY»,
        // начиная с «B» пропускаем 8 символов (т.е. с 9-го) и берём 13 знаков.
        private const string IbanPrefix = "BY";
        private const int IbanSkip = 8;
        private const int IbanLength = 13;

        // ---------------------------------------------------------------------------

        /// <param name="kind">Распознанный тип документа (для статуса).</param>
        public static bool TryExtract(string text, out string result, out string kind, out string error)
        {
            result = "";
            kind = "";

            if (RevocationRx.IsMatch(text))
            {
                kind = "Отзыв документа";
                return ExtractIban(text, out result, out error);
            }

            if (StatementRx.IsMatch(text))
            {
                if (SuspendedRx.IsMatch(text))
                {
                    kind = "Заявление (приостановление)";
                    return ExtractAfterMarker(text, SuspendedOccurrence, SuspendedSkip, SuspendedLength, out result, out error);
                }

                kind = "Заявление";
                return ExtractAfterMarker(text, StatementOccurrence, StatementSkip, StatementLength, out result, out error);
            }

            error = "Тип документа не определён (нет «Отзыв документа» / «Заявление»)";
            return false;
        }

        private static bool ExtractIban(string text, out string result, out string error)
        {
            result = "";
            Match label = PayerAccountRx.Match(text);
            if (!label.Success)
            {
                error = "Не найден текст «Номер счета плательщика»";
                return false;
            }

            int by = text.IndexOf(IbanPrefix, label.Index + label.Length, StringComparison.Ordinal);
            if (by < 0)
            {
                error = "После «Номер счета плательщика» нет «" + IbanPrefix + "»";
                return false;
            }

            if (text.Length - by < IbanSkip + IbanLength)
            {
                error = "После «" + IbanPrefix + "» слишком мало символов";
                return false;
            }

            result = text.Substring(by + IbanSkip, IbanLength);
            error = "";
            return true;
        }

        private static bool ExtractAfterMarker(string text, int occurrence, int skip, int length, out string result, out string error)
        {
            result = "";
            int idx = -1;
            int found = 0;
            while (found < occurrence)
            {
                idx = text.IndexOf(Marker, idx + 1);
                if (idx < 0)
                {
                    error = found == 0
                        ? "В буфере нет символа «" + Marker + "»"
                        : "В буфере только " + found + " симв. «" + Marker + "», а нужен №" + occurrence;
                    return false;
                }
                found++;
            }

            string after = text.Substring(idx + 1);
            if (after.Length < TakeAfterMarker)
            {
                error = "После «" + Marker + "» только " + after.Length + " симв., нужно " + TakeAfterMarker;
                return false;
            }

            result = after.Substring(0, TakeAfterMarker).Substring(skip, length);
            error = "";
            return true;
        }
    }

    internal static class F1Keys
    {
        public const ushort VK_RETURN = 0x0D;
        public const ushort VK_RIGHT = 0x27;
        public const ushort VK_DOWN = 0x28;
        public const ushort VK_BACK = 0x08;
        public const ushort VK_TAB = 0x09;
        public const ushort VK_F7 = 0x76;
        public const ushort VK_CONTROL = 0x11;
        public const ushort VK_LEFT = 0x25;
        public const ushort VK_A = 0x41;
        public const ushort VK_V = 0x56;

        /// <summary>Ctrl+A: выделить всё.</summary>
        public static Task CtrlA()
        {
            return CtrlKey(VK_A);
        }

        /// <summary>Ctrl+V: вставить из буфера обмена.</summary>
        public static Task CtrlV()
        {
            return CtrlKey(VK_V);
        }

        private static async Task CtrlKey(ushort vk)
        {
            // Клавиши отправляются по одной с паузами: многие приложения не успевают
            // увидеть зажатый Ctrl, если комбинация приходит одним пакетом.
            Send(KeyInput(VK_CONTROL, 0));
            await Task.Delay(50);
            Send(KeyInput(vk, 0));
            await Task.Delay(50);
            Send(KeyInput(vk, F1Native.KEYEVENTF_KEYUP));
            await Task.Delay(50);
            Send(KeyInput(VK_CONTROL, F1Native.KEYEVENTF_KEYUP));
        }

        /// <summary>Набор текста Unicode-символами (работает с любой раскладкой).</summary>
        public static void TypeUnicode(string s)
        {
            var inputs = new F1Native.INPUT[s.Length * 2];
            for (int i = 0; i < s.Length; i++)
            {
                inputs[i * 2] = UnicodeInput(s[i], F1Native.KEYEVENTF_UNICODE);
                inputs[i * 2 + 1] = UnicodeInput(s[i], F1Native.KEYEVENTF_UNICODE | F1Native.KEYEVENTF_KEYUP);
            }
            Send(inputs);
        }

        private static F1Native.INPUT UnicodeInput(char c, uint flags)
        {
            var input = new F1Native.INPUT();
            input.type = F1Native.INPUT_KEYBOARD;
            input.U.ki.wScan = c;
            input.U.ki.dwFlags = flags;
            return input;
        }

        /// <summary>Клик правой кнопкой мыши в текущей позиции курсора.</summary>
        public static void RightClick()
        {
            Send(MouseInput(F1Native.MOUSEEVENTF_RIGHTDOWN), MouseInput(F1Native.MOUSEEVENTF_RIGHTUP));
        }

        /// <summary>Нажатие и отпускание клавиши. Для стрелок нужен флаг extended.</summary>
        public static void Press(ushort vk, bool extended)
        {
            uint ext = extended ? F1Native.KEYEVENTF_EXTENDEDKEY : 0;
            Send(KeyInput(vk, ext), KeyInput(vk, ext | F1Native.KEYEVENTF_KEYUP));
        }

        private static void Send(params F1Native.INPUT[] inputs)
        {
            uint sent = F1Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(F1Native.INPUT)));
            if (sent != inputs.Length)
                throw new InvalidOperationException("SendInput не сработал (если целевое окно запущено от администратора — запустите программу тоже от администратора)");
        }

        private static F1Native.INPUT MouseInput(uint flags)
        {
            var input = new F1Native.INPUT();
            input.type = F1Native.INPUT_MOUSE;
            input.U.mi.dwFlags = flags;
            return input;
        }

        private static F1Native.INPUT KeyInput(ushort vk, uint flags)
        {
            var input = new F1Native.INPUT();
            input.type = F1Native.INPUT_KEYBOARD;
            input.U.ki.wVk = vk;
            input.U.ki.wScan = (ushort)F1Native.MapVirtualKey(vk, 0); // скан-код, нужен части приложений
            input.U.ki.dwFlags = flags;
            return input;
        }
    }

    internal static class F1Native
    {
        public const uint INPUT_MOUSE = 0;
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;
        public const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        public static bool IsShiftDown()
        {
            return (GetAsyncKeyState(0x10) & 0x8000) != 0; // VK_SHIFT
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        /// <summary>Заголовок активного окна (пустая строка, если окна нет).</summary>
        public static string GetActiveWindowTitle()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return "";
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        // Union должен включать самый большой член (MOUSEINPUT), иначе sizeof(INPUT) неверен на x64.
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }
    }
}
