using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

// Добавьте этот файл в проект (Проект -> Добавить существующий элемент).
// Form1 уже partial, поэтому ваш Form1.cs и дизайнер менять не нужно.
namespace WindowsFormsApp1
{
    public partial class Form1
    {
        private const int F1HotkeyId = 0xF1;
        private const uint VK_F1 = 0x70;

        private const int MenuDelayMs = 200;  // пауза после ПКМ, пока откроется меню
        private const int StepDelayMs = 100;  // пауза между нажатиями клавиш
        private const int ClipboardTimeoutMs = 2000;

        private bool _f1Busy;
        private NumericUpDown _f1Occurrence;

        // Номер символа №, после которого берём текст (1 = первый). Хранится рядом с exe.
        private static string F1OccurrenceFile
        {
            get { return Path.Combine(Application.StartupPath, "f1_marker.txt"); }
        }

        // Поле «Номер №» добавляется на форму кодом в левый нижний угол.
        // Если оно перекрывает ваши элементы — поменяйте Location.
        private void CreateF1OccurrenceControl()
        {
            if (_f1Occurrence != null) return;

            var label = new Label
            {
                Text = "Номер №:",
                AutoSize = true,
                Location = new System.Drawing.Point(10, ClientSize.Height - 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _f1Occurrence = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 99,
                Value = 1,
                Width = 50,
                Location = new System.Drawing.Point(75,ClientSize.Height - 29),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            try
            {
                int saved;
                if (File.Exists(F1OccurrenceFile) && int.TryParse(File.ReadAllText(F1OccurrenceFile).Trim(), out saved))
                    _f1Occurrence.Value = Math.Max(1, Math.Min(99, saved));
            }
            catch { /* файл не прочитался — остаётся 1 */ }

            _f1Occurrence.ValueChanged += delegate
            {
                try { File.WriteAllText(F1OccurrenceFile, _f1Occurrence.Value.ToString()); }
                catch { }
            };

            Controls.Add(label);
            Controls.Add(_f1Occurrence);
            label.BringToFront();
            _f1Occurrence.BringToFront();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CreateF1OccurrenceControl();
            if (!F1Native.RegisterHotKey(Handle, F1HotkeyId, F1Native.MOD_NOREPEAT, VK_F1))
                ShowF1Status("Не удалось зарегистрировать F1 (занята другой программой)");
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            F1Native.UnregisterHotKey(Handle, F1HotkeyId);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == F1Native.WM_HOTKEY && m.WParam.ToInt32() == F1HotkeyId)
            {
                var ignored = RunF1ScenarioAsync();
                return;
            }
            base.WndProc(ref m);
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

                string result, error;
                if (!F1ClipboardProcessor.TryExtract(Clipboard.GetText(), (int)_f1Occurrence.Value, out result, out error))
                {
                    ShowF1Status(error);
                    return;
                }

                Clipboard.SetText(result);
                ShowF1Status("Скопировано: " + result);
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
        // Настройки правила — меняйте здесь.
        private const char Marker = '№';
        private const int TakeAfterMarker = 28; // сколько символов берём после №
        private const int SkipChars = 8;        // «с 9-го символа» => пропускаем 8
        private const int ResultLength = 12;    // сколько символов копируем

        /// <param name="occurrence">Какой по счёту № использовать (1 = первый).</param>
        public static bool TryExtract(string text, int occurrence, out string result, out string error)
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

            string block = after.Substring(0, TakeAfterMarker);
            result = block.Substring(SkipChars, ResultLength);
            error = "";
            return true;
        }
    }

    internal static class F1Keys
    {
        public const ushort VK_RETURN = 0x0D;
        public const ushort VK_RIGHT = 0x27;
        public const ushort VK_DOWN = 0x28;
        public const ushort VK_CONTROL = 0x11;
        public const ushort VK_A = 0x41;

        /// <summary>Ctrl+A: выделить всё.</summary>
        public static async Task CtrlA()
        {
            // Клавиши отправляются по одной с паузами: многие приложения не успевают
            // увидеть зажатый Ctrl, если Ctrl+A приходит одним пакетом.
            Send(KeyInput(VK_CONTROL, 0));
            await Task.Delay(50);
            Send(KeyInput(VK_A, 0));
            await Task.Delay(50);
            Send(KeyInput(VK_A, F1Native.KEYEVENTF_KEYUP));
            await Task.Delay(50);
            Send(KeyInput(VK_CONTROL, F1Native.KEYEVENTF_KEYUP));
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
        public const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
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
