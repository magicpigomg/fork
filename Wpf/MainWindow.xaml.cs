using System.Windows.Interop;
using MultiTool.Models;
using MultiTool.Services;
using MultiTool.Views;

namespace MultiTool;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, (string Title, string Subtitle, Func<UserControl> Create)> _pages;
    private readonly Dictionary<string, UserControl> _cache = new();

    public MainWindow()
    {
        InitializeComponent();

        _pages = new()
        {
            [nameof(NavHotkeys)] = ("Горячие клавиши", "Сценарии на F1–F3 и Shift+F1–F3 работают в любом окне", () => new HotkeysView()),
            [nameof(NavCompare)] = ("Сравнение текстов", "Посимвольная проверка двух значений", () => new CompareView()),
            [nameof(NavTools)] = ("Инструменты", "Очистка пробелов и разбор номера", () => new ToolsView()),
            [nameof(NavFiles)] = ("Файлы", "Создание файлов приостановления и Accept", () => new FilesView()),
            [nameof(NavSettings)] = ("Настройки", "Все параметры хранятся в " + SettingsStore.Folder, () => new SettingsView()),
        };

        AppServices.HotkeysStateChanged += UpdateStatus;
        UpdateStatus();
        Navigate(nameof(NavHotkeys));

        SourceInitialized += (_, _) => ApplyWindowBorder();
        StateChanged += (_, _) => UpdateWindowState();
        Closing += (_, _) => SettingsStore.Save(AppSettings.Current);
    }

    // ── Собственный заголовок окна ───────────────────────────────────────────────────────

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

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
        if (sender is RadioButton { Name: { } name } && IsLoaded)
            Navigate(name);
    }

    private void Navigate(string key)
    {
        var (title, subtitle, create) = _pages[key];
        if (!_cache.TryGetValue(key, out var view))
            _cache[key] = view = create();

        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
        Host.Content = view;
    }

    private void UpdateStatus()
    {
        bool on = AppServices.HotkeysActive;
        StatusDot.Fill = (System.Windows.Media.Brush)FindResource(on ? "SuccessBrush" : "MutedBrush");
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
