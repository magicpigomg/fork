using MultiTool.Models;
using MultiTool.Views;
using static MultiTool.Services.KeySender;

namespace MultiTool.Services;

/// <summary>Сценарии горячих клавиш: F1 (копирование и разбор), F2, F3 (последовательности клавиш),
/// Shift+F1..F3 (ввод готовых текстов).</summary>
public sealed class ScenarioService
{
    private readonly HotkeyService _hotkeys;
    private bool _busy;

    public ScenarioService(HotkeyService hotkeys)
    {
        _hotkeys = hotkeys;
        _hotkeys.Pressed += OnPressed;
    }

    private async void OnPressed(HotkeyKind kind)
    {
        try
        {
            await HandleAsync(kind);
        }
        catch (Exception ex)
        {
            Report(kind, ex.Message, LogKind.Error);
        }
    }

    private async Task HandleAsync(HotkeyKind kind)
    {
        var settings = AppSettings.Current;

        // В «Сеанс A–D» и в самом приложении сценарии не выполняются, клавиша работает как обычная.
        if (NativeMethods.IsOwnWindowActive() || IsBlockedWindowActive(settings))
        {
            await PassThroughAsync(kind, settings);
            return;
        }

        if (_busy) return;
        _busy = true;
        try
        {
            switch (kind)
            {
                case HotkeyKind.F1: await RunF1Async(settings); break;
                case HotkeyKind.F2: await RunF2Async(settings); break;
                case HotkeyKind.F3: await RunF3Async(settings); break;
                default: await TypeTextAsync(kind, settings); break;
            }
        }
        catch (Exception ex)
        {
            Report(kind, ex.Message, LogKind.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    // ── F1: Ctrl+A, ПКМ, ↓, →, Enter, затем разбор буфера по типу документа ────────────────

    private async Task RunF1Async(AppSettings s)
    {
        uint seqBefore = NativeMethods.GetClipboardSequenceNumber();

        await CtrlAsync(VK_A);
        await Task.Delay(s.StepDelayMs);
        RightClick();
        await Task.Delay(s.MenuDelayMs);
        Press(VK_DOWN, extended: true);
        await Task.Delay(s.StepDelayMs);
        Press(VK_RIGHT, extended: true);
        await Task.Delay(s.StepDelayMs);
        Press(VK_RETURN);

        // Ждём, пока пункт меню положит данные в буфер (если буфер не изменился — берём то, что есть).
        for (int waited = 0; waited < s.ClipboardTimeoutMs; waited += 50)
        {
            if (NativeMethods.GetClipboardSequenceNumber() != seqBefore) break;
            await Task.Delay(50);
        }
        await Task.Delay(50);

        string? text = await ClipboardHelper.GetTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            Report(HotkeyKind.F1, "В буфере нет текста", LogKind.Error);
            return;
        }

        ExtractResult result = ClipboardProcessor.Extract(text, s);
        if (!result.Success)
        {
            Report(HotkeyKind.F1, result.Error, LogKind.Error);
            return;
        }

        if (!await ClipboardHelper.SetTextAsync(result.Value))
        {
            Report(HotkeyKind.F1, "Буфер обмена занят другой программой", LogKind.Error);
            return;
        }

        ActivityLog.SetLastValue(result.Value);
        Report(HotkeyKind.F1, $"{result.Kind}: {result.Value}", LogKind.Success);
    }

    // ── F2: F7, Tab×3, Backspace, Tab×8, →, Tab×3, Enter ─────────────────────────────────

    private async Task RunF2Async(AppSettings s)
    {
        Press(VK_F7);
        await Task.Delay(s.F7DelayMs);

        await PressManyAsync(VK_TAB, 3, s.StepDelayMs);
        Press(VK_BACK);
        await Task.Delay(s.StepDelayMs);

        await PressManyAsync(VK_TAB, 8, s.StepDelayMs);
        Press(VK_RIGHT, extended: true);
        await Task.Delay(s.StepDelayMs);

        await PressManyAsync(VK_TAB, 3, s.StepDelayMs);
        Press(VK_RETURN);

        Report(HotkeyKind.F2, "Последовательность выполнена", LogKind.Success);
    }

    // ── F3: F7, Tab×4, Ctrl+V, Tab×8, ←, Tab×3, Enter ────────────────────────────────────

    private async Task RunF3Async(AppSettings s)
    {
        Press(VK_F7);
        await Task.Delay(s.F7DelayMs);

        await PressManyAsync(VK_TAB, 4, s.StepDelayMs);
        await CtrlAsync(VK_V);
        await Task.Delay(s.StepDelayMs);

        await PressManyAsync(VK_TAB, 8, s.StepDelayMs);
        Press(VK_LEFT, extended: true);
        await Task.Delay(s.StepDelayMs);

        await PressManyAsync(VK_TAB, 3, s.StepDelayMs);
        Press(VK_RETURN);

        Report(HotkeyKind.F3, "Последовательность выполнена", LogKind.Success);
    }

    // ── Shift+F1..F3: ввод текста ────────────────────────────────────────────────────────

    private async Task TypeTextAsync(HotkeyKind kind, AppSettings s)
    {
        string text = HotkeyInfo.FNumber(kind) switch
        {
            1 => s.ShiftText1,
            2 => s.ShiftText2,
            _ => s.ShiftText3
        };
        if (string.IsNullOrEmpty(text))
        {
            Report(kind, "Текст для этой клавиши не задан в настройках", LogKind.Error);
            return;
        }

        // Ждём, пока пользователь отпустит Shift, иначе символы придут с зажатым Shift.
        for (int i = 0; i < 30 && NativeMethods.IsShiftDown(); i++)
            await Task.Delay(50);

        const int chunk = 8;
        for (int pos = 0; pos < text.Length; pos += chunk)
        {
            TypeUnicode(text.Substring(pos, Math.Min(chunk, text.Length - pos)));
            await Task.Delay(10);
        }

        Report(kind, "Текст введён", LogKind.Success);
    }

    // ── Блокировка окон и пересылка клавиши ──────────────────────────────────────────────

    private static bool IsBlockedWindowActive(AppSettings s)
    {
        string title = NativeMethods.GetActiveWindowTitle();
        foreach (string blocked in s.BlockedWindowList)
        {
            if (title.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // RegisterHotKey «съедает» клавишу, поэтому в заблокированном окне отправляем её заново:
    // на мгновение снимаем горячую клавишу, нажимаем F-клавишу и регистрируем снова.
    // Для Shift+F-клавиш Shift физически зажат, поэтому окно получит именно Shift+F.
    private async Task PassThroughAsync(HotkeyKind kind, AppSettings s)
    {
        if (!s.PassKeyThroughWhenBlocked) return;

        _hotkeys.Suspend(kind);
        try
        {
            Press(HotkeyInfo.Vk(kind));
            await Task.Delay(100);
        }
        finally
        {
            _hotkeys.Resume(kind);
        }
    }

    private static void Report(HotkeyKind kind, string message, LogKind logKind)
    {
        string source = HotkeyInfo.Name(kind);
        ActivityLog.Add(source, message, logKind);
        if (AppSettings.Current.ShowToasts)
            ToastWindow.ShowToast(source, message, logKind);
    }
}
