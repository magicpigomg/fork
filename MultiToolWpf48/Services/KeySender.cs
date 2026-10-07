using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MultiTool.Services
{
    internal static class KeySender
    {
        public const ushort VK_BACK = 0x08;
        public const ushort VK_TAB = 0x09;
        public const ushort VK_RETURN = 0x0D;
        public const ushort VK_CONTROL = 0x11;
        public const ushort VK_LEFT = 0x25;
        public const ushort VK_RIGHT = 0x27;
        public const ushort VK_DOWN = 0x28;
        public const ushort VK_9 = 0x39;
        public const ushort VK_A = 0x41;
        public const ushort VK_V = 0x56;
        public const ushort VK_F7 = 0x76;

        /// <summary>Клик правой кнопкой мыши в текущей позиции курсора.</summary>
        public static void RightClick()
        {
            Send(MouseInput(NativeMethods.MOUSEEVENTF_RIGHTDOWN), MouseInput(NativeMethods.MOUSEEVENTF_RIGHTUP));
        }

        /// <summary>Нажатие и отпускание клавиши. Для стрелок нужен флаг extended.</summary>
        public static void Press(ushort vk, bool extended = false)
        {
            uint ext = extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0;
            Send(KeyInput(vk, ext), KeyInput(vk, ext | NativeMethods.KEYEVENTF_KEYUP));
        }

        /// <summary>Нажатие клавиши vk несколько раз подряд с паузой между нажатиями.</summary>
        public static async Task PressManyAsync(ushort vk, int count, int delayMs, bool extended = false)
        {
            for (int i = 0; i < count; i++)
            {
                Press(vk, extended);
                await Task.Delay(delayMs);
            }
        }

        /// <summary>Ctrl+клавиша. Клавиши отправляются по одной с паузами: многие приложения
        /// не успевают увидеть зажатый Ctrl, если комбинация приходит одним пакетом.</summary>
        public static async Task CtrlAsync(ushort vk)
        {
            Send(KeyInput(VK_CONTROL, 0));
            await Task.Delay(50);
            Send(KeyInput(vk, 0));
            await Task.Delay(50);
            Send(KeyInput(vk, NativeMethods.KEYEVENTF_KEYUP));
            await Task.Delay(50);
            Send(KeyInput(VK_CONTROL, NativeMethods.KEYEVENTF_KEYUP));
        }

        /// <summary>Набор текста Unicode-символами (не зависит от раскладки, буфер обмена не трогает).</summary>
        public static void TypeUnicode(string text)
        {
            var inputs = new NativeMethods.INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                inputs[i * 2] = UnicodeInput(text[i], NativeMethods.KEYEVENTF_UNICODE);
                inputs[i * 2 + 1] = UnicodeInput(text[i], NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP);
            }
            Send(inputs);
        }

        private static void Send(params NativeMethods.INPUT[] inputs)
        {
            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sent != inputs.Length)
                throw new InvalidOperationException(
                    "SendInput не сработал. Если целевое окно запущено от администратора — запустите MultiTool тоже от администратора.");
        }

        private static NativeMethods.INPUT MouseInput(uint flags)
        {
            var input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.U.mi.dwFlags = flags;
            return input;
        }

        private static NativeMethods.INPUT KeyInput(ushort vk, uint flags)
        {
            var input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.U.ki.wVk = vk;
            input.U.ki.wScan = (ushort)NativeMethods.MapVirtualKey(vk, 0); // скан-код нужен части приложений
            input.U.ki.dwFlags = flags;
            return input;
        }

        private static NativeMethods.INPUT UnicodeInput(char c, uint flags)
        {
            var input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_KEYBOARD;
            input.U.ki.wScan = c;
            input.U.ki.dwFlags = flags;
            return input;
        }
    }
}
