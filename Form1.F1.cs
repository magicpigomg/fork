using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiTool;

public sealed class MainForm : Form
{
    private const int HotkeyId = 1;
    private const uint HotkeyVk = 0x70; // F1

    private const int MenuDelayMs = 200;  // пауза после ПКМ, пока откроется меню
    private const int StepDelayMs = 100;  // пауза между нажатиями клавиш
    private const int ClipboardTimeoutMs = 2000;

    private readonly Label _status = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Text = "F1: ПКМ → ↓ → → → Enter → поиск № в буфере"
    };

    private bool _busy;

    public MainForm()
    {
        Text = "MultiTool";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 80);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;
        Controls.Add(_status);
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
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
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
            uint seqBefore = NativeMethods.GetClipboardSequenceNumber();

            KeySender.RightClick();
            await Task.Delay(MenuDelayMs);
            KeySender.Press(KeySender.VK_DOWN, extended: true);
            await Task.Delay(StepDelayMs);
            KeySender.Press(KeySender.VK_RIGHT, extended: true);
            await Task.Delay(StepDelayMs);
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
}
