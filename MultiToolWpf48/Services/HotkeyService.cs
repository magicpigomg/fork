using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace MultiTool.Services
{
    /// <summary>Номера совпадают с id горячих клавиш в Windows.</summary>
    public enum HotkeyKind { F1 = 0xF1, F2 = 0xF2, F3 = 0xF3, ShiftF1 = 0xF4, ShiftF2 = 0xF5, ShiftF3 = 0xF6 }

    public static class HotkeyInfo
    {
        public static IEnumerable<HotkeyKind> All
        {
            get { return Enum.GetValues(typeof(HotkeyKind)).Cast<HotkeyKind>(); }
        }

        public static bool IsShift(HotkeyKind kind) { return kind >= HotkeyKind.ShiftF1; }

        /// <summary>Номер F-клавиши: 1..3.</summary>
        public static int FNumber(HotkeyKind kind) { return ((int)kind - (int)HotkeyKind.F1) % 3 + 1; }

        public static ushort Vk(HotkeyKind kind) { return (ushort)(0x70 + FNumber(kind) - 1); } // VK_F1 = 0x70

        public static uint Modifiers(HotkeyKind kind) { return IsShift(kind) ? NativeMethods.MOD_SHIFT : 0; }

        public static string Name(HotkeyKind kind) { return (IsShift(kind) ? "Shift+" : "") + "F" + FNumber(kind); }
    }

    /// <summary>Глобальные горячие клавиши через скрытое окно-приёмник сообщений.</summary>
    public sealed class HotkeyService : IDisposable
    {
        private HwndSource _source;
        private readonly HashSet<HotkeyKind> _registered = new HashSet<HotkeyKind>();

        public bool IsEnabled { get; private set; }

        public event Action<HotkeyKind> Pressed;

        public void Start()
        {
            var parameters = new HwndSourceParameters("MultiToolHotkeys")
            {
                ParentWindow = NativeMethods.HWND_MESSAGE, // окно без интерфейса, только получает сообщения
                Width = 0,
                Height = 0
            };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);
        }

        /// <summary>Включает или выключает все горячие клавиши. Возвращает список клавиш, которые занять не удалось.</summary>
        public IReadOnlyList<HotkeyKind> SetEnabled(bool enabled)
        {
            var failed = new List<HotkeyKind>();
            if (_source == null) return failed;

            foreach (var kind in HotkeyInfo.All)
            {
                if (enabled)
                {
                    if (!Register(kind)) failed.Add(kind);
                }
                else
                {
                    Unregister(kind);
                }
            }
            IsEnabled = enabled;
            return failed;
        }

        /// <summary>Временно отпустить клавишу (чтобы отправить её в окно как обычную).</summary>
        public void Suspend(HotkeyKind kind) { Unregister(kind); }

        public void Resume(HotkeyKind kind)
        {
            if (IsEnabled) Register(kind);
        }

        private bool Register(HotkeyKind kind)
        {
            if (_source == null) return false;
            if (_registered.Contains(kind)) return true;

            bool ok = NativeMethods.RegisterHotKey(_source.Handle, (int)kind,
                HotkeyInfo.Modifiers(kind) | NativeMethods.MOD_NOREPEAT, HotkeyInfo.Vk(kind));
            if (ok) _registered.Add(kind);
            return ok;
        }

        private void Unregister(HotkeyKind kind)
        {
            if (_source == null || !_registered.Remove(kind)) return;
            NativeMethods.UnregisterHotKey(_source.Handle, (int)kind);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (Enum.IsDefined(typeof(HotkeyKind), id))
                {
                    handled = true;
                    var handler = Pressed;
                    if (handler != null) handler((HotkeyKind)id);
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source == null) return;
            SetEnabled(false);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
