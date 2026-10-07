using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MultiTool.Models;
using MultiTool.Services;

namespace MultiTool.Views
{
    /// <summary>Всплывающее уведомление в углу экрана. Не забирает фокус у окна, в которое идёт ввод.</summary>
    public partial class ToastWindow : Window
    {
        private static ToastWindow _instance;

        private readonly DispatcherTimer _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };

        public static void ShowToast(string source, string message, LogKind kind)
        {
            if (_instance == null) _instance = new ToastWindow();
            _instance.Display(source, message, kind);
        }

        private ToastWindow()
        {
            InitializeComponent();
            _hideTimer.Tick += delegate
            {
                _hideTimer.Stop();
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
                fade.Completed += delegate { if (Opacity == 0) Hide(); };
                BeginAnimation(OpacityProperty, fade);
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // WS_EX_NOACTIVATE: окно не становится активным и не крадёт фокус; TOOLWINDOW — не в Alt+Tab.
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
        }

        private void Display(string source, string message, LogKind kind)
        {
            SourceText.Text = source;
            MessageText.Text = message;

            string brushKey = kind == LogKind.Success ? "SuccessBrush" : kind == LogKind.Error ? "DangerBrush" : "AccentBrush";
            Dot.Fill = (Brush)FindResource(brushKey);

            BeginAnimation(OpacityProperty, null); // сбросить затухание от предыдущего тоста
            Opacity = 1;

            UpdateLayout();
            Rect area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth;
            Top = area.Bottom - ActualHeight;

            if (!IsVisible) Show();

            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }
}
