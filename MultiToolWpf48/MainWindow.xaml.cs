using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MultiTool.Models;
using MultiTool.Services;
using MultiTool.Views;

namespace MultiTool
{
    public partial class MainWindow : Window
    {
        private sealed class PageInfo
        {
            public string Title;
            public string Subtitle;
            public Func<UserControl> Create;
        }

        private readonly Dictionary<string, PageInfo> _pages;
        private readonly Dictionary<string, UserControl> _cache = new Dictionary<string, UserControl>();

        public MainWindow()
        {
            InitializeComponent();

            _pages = new Dictionary<string, PageInfo>
            {
                { "NavHotkeys", new PageInfo { Title = "Горячие клавиши", Subtitle = "Сценарии на F1–F3 и Shift+F1–F3 работают в любом окне", Create = () => new HotkeysView() } },
                { "NavCompare", new PageInfo { Title = "Сравнение текстов", Subtitle = "Посимвольная проверка двух значений", Create = () => new CompareView() } },
                { "NavTools", new PageInfo { Title = "Инструменты", Subtitle = "Очистка пробелов и разбор номера", Create = () => new ToolsView() } },
                { "NavFiles", new PageInfo { Title = "Файлы", Subtitle = "Создание файлов приостановления и Accept", Create = () => new FilesView() } },
                { "NavSettings", new PageInfo { Title = "Настройки", Subtitle = "Все параметры хранятся в " + SettingsStore.Folder, Create = () => new SettingsView() } },
            };

            AppServices.HotkeysStateChanged += UpdateStatus;
            UpdateStatus();
            Navigate("NavHotkeys");

            SourceInitialized += delegate { ApplyWindowBorder(); };
            StateChanged += delegate { UpdateWindowState(); };
            Closing += delegate { SettingsStore.Save(AppSettings.Current); };
        }

        // ── Собственный заголовок окна ───────────────────────────────────────────────────────

        private void OnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestore(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateWindowState()
        {
            bool maximized = WindowState == WindowState.Maximized;
            MaxButton.Content = maximized ? "" : ""; // «восстановить» / «развернуть»
            MaxButton.ToolTip = maximized ? "Восстановить" : "Развернуть";

            // В развёрнутом виде окно с WindowChrome выходит за экран на толщину рамки — компенсируем отступом.
            WindowBorder.Padding = maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
        }

        private void OnNavChecked(object sender, RoutedEventArgs e)
        {
            var radio = sender as RadioButton;
            if (radio != null && IsLoaded)
                Navigate(radio.Name);
        }

        private void Navigate(string key)
        {
            PageInfo page = _pages[key];
            UserControl view;
            if (!_cache.TryGetValue(key, out view))
                _cache[key] = view = page.Create();

            PageTitle.Text = page.Title;
            PageSubtitle.Text = page.Subtitle;
            Host.Content = view;
        }

        private void UpdateStatus()
        {
            bool on = AppServices.HotkeysActive;
            StatusDot.Fill = (Brush)FindResource(on ? "SuccessBrush" : "MutedBrush");
            StatusTitle.Text = on ? "Клавиши активны" : "Клавиши выключены";
            StatusSub.Text = on ? "F1–F3, Shift+F1–F3" : "Включите на главной";
        }

        /// <summary>Цвет тонкой рамки окна под тему (Windows 11; на других системах вызов безвреден).</summary>
        private void ApplyWindowBorder()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));   // DWMWA_USE_IMMERSIVE_DARK_MODE
            int border = 0x00382C27;                                                // COLORREF 0x00BBGGRR для #272C38
            NativeMethods.DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int)); // DWMWA_BORDER_COLOR
        }
    }
}
