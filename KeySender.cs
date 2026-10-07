using System.Runtime.InteropServices;

namespace MultiTool;

internal static class KeySender
{
    public const ushort VK_RETURN = 0x0D;
    public const ushort VK_RIGHT = 0x27;
    public const ushort VK_DOWN = 0x28;

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

    private static void Send(params NativeMethods.INPUT[] inputs)
    {
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
            throw new InvalidOperationException("SendInput не сработал (возможно, целевое окно запущено от администратора — запустите MultiTool тоже от администратора).");
    }

    private static NativeMethods.INPUT MouseInput(uint flags) => new()
    {
        type = NativeMethods.INPUT_MOUSE,
        U = new NativeMethods.InputUnion { mi = new NativeMethods.MOUSEINPUT { dwFlags = flags } }
    };

    private static NativeMethods.INPUT KeyInput(ushort vk, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = flags } }
    };
}
