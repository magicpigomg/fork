using System.Text.Json;

namespace MultiTool;

public sealed class MainForm : Form
{
    private sealed class Settings
    {
        public string Target { get; set; } = "";
        public int MenuDelayMs { get; set; } = 200;
        public int StepDelayMs { get; set; } = 100;
    }

    private const int HotkeyId = 1;
    private const uint HotkeyVk = 0x70; // F1
    private const int ClipboardTimeoutMs = 2000;

    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");

    private readonly TextBox _target = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _menuDelay = new() { Minimum = 0, Maximum = 10000, Increment = 50, Dock = DockStyle.Left, Width = 80 };
    private readonly NumericUpDown _stepDelay = new() { Minimum = 0, Maximum = 10000, Increment = 50, Dock = DockStyle.Left, Width = 80 };
    private readonly Button _btnClip = new() { Text = "Только обработать буфер обмена", Dock = DockStyle.Fill, Height = 36 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = false };

    private bool _busy;

    public MainForm()
    {
        Text = "MultiTool — F1";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 250);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(10) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Фокус на окно (необязательно):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_target, 1, 0);
        layout.Controls.Add(new Label { Text = "Пауза после ПКМ, мс:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_menuDelay, 1, 1);
        layout.Controls.Add(new Label { Text = "Пауза между клавишами, мс:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_stepDelay, 1, 2);
        layout.Controls.Add(new Label
        {
            Text = "F1 (глобально): ПКМ → ↓ → → → Enter → поиск № в буфере.",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 3);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 3)!, 2);
        layout.Controls.Add(_btnClip, 0, 4);
        layout.SetColumnSpan(_btnClip, 2);
        layout.Controls.Add(_status, 0, 5);
        layout.SetColumnSpan(_status, 2);
        Controls.Add(layout);

        _btnClip.Click += (_, _) => ProcessClipboard();
        Load += (_, _) => LoadSettings();
        FormClosing += (_, _) => SaveSettings();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!NativeMethods.RegisterHotKey(Handle, HotkeyId, NativeMethods.MOD_NOREPEAT, HotkeyVk))
            SetStatus("Не удалось зарегистрировать F1 (занята другой программой).", true);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam == HotkeyId)
            _ = RunScenarioAsync();
        else
            base.WndProc(ref m);
    }

    private async Task RunScenarioAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (_target.Text.Trim().Length > 0)
            {
                IntPtr hwnd = WindowFocus.FindWindow(_target.Text);
                if (hwnd == IntPtr.Zero) { SetStatus($"Окно «{_target.Text}» не найдено.", true); return; }
                if (!WindowFocus.Focus(hwnd)) { SetStatus("Не удалось вывести окно на передний план.", true); return; }
                await Task.Delay(150);
            }

            int step = (int)_stepDelay.Value;
            uint seqBefore = NativeMethods.GetClipboardSequenceNumber();

            KeySender.RightClick();
            await Task.Delay((int)_menuDelay.Value);
            KeySender.Press(KeySender.VK_DOWN, extended: true);
            await Task.Delay(step);
            KeySender.Press(KeySender.VK_RIGHT, extended: true);
            await Task.Delay(step);
            KeySender.Press(KeySender.VK_RETURN);

            // Ждём, пока пункт меню положит данные в буфер (если буфер не изменился — берём то, что есть).
            for (int waited = 0; waited < ClipboardTimeoutMs; waited += 50)
            {
                if (NativeMethods.GetClipboardSequenceNumber() != seqBefore) break;
                await Task.Delay(50);
            }
            await Task.Delay(50);

            ProcessClipboard();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ProcessClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) { SetStatus("В буфере нет текста.", true); return; }

            if (!ClipboardProcessor.TryExtract(Clipboard.GetText(), out string result, out string error))
            {
                SetStatus(error, true);
                return;
            }

            Clipboard.SetText(result);
            SetStatus($"Скопировано: {result}", false);
        }
        catch (Exception ex)
        {
            // Буфер может быть временно занят другим процессом.
            SetStatus(ex.Message, true);
        }
    }

    private void SetStatus(string text, bool error)
    {
        _status.ForeColor = error ? Color.Firebrick : Color.DarkGreen;
        _status.Text = text;
    }

    private void LoadSettings()
    {
        var s = new Settings();
        try
        {
            if (File.Exists(SettingsPath))
                s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? s;
        }
        catch { /* битый файл настроек — используем значения по умолчанию */ }

        _target.Text = s.Target;
        _menuDelay.Value = Math.Clamp(s.MenuDelayMs, (int)_menuDelay.Minimum, (int)_menuDelay.Maximum);
        _stepDelay.Value = Math.Clamp(s.StepDelayMs, (int)_stepDelay.Minimum, (int)_stepDelay.Maximum);
    }

    private void SaveSettings()
    {
        try
        {
            var s = new Settings { Target = _target.Text, MenuDelayMs = (int)_menuDelay.Value, StepDelayMs = (int)_stepDelay.Value };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
