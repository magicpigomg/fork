using MultiTool.Models;

namespace MultiTool.Services;

/// <summary>Общие службы приложения: горячие клавиши и сценарии.</summary>
public static class AppServices
{
    private static HotkeyService? _hotkeys;
    private static ScenarioService? _scenarios;

    /// <summary>Горячие клавиши включены или выключены (для индикаторов в интерфейсе).</summary>
    public static bool HotkeysActive => _hotkeys?.IsEnabled ?? false;

    public static event Action? HotkeysStateChanged;

    public static void Start()
    {
        _hotkeys = new HotkeyService();
        _hotkeys.Start();
        _scenarios = new ScenarioService(_hotkeys);
        SetHotkeysEnabled(AppSettings.Current.HotkeysEnabled);
    }

    public static void Stop()
    {
        _hotkeys?.Dispose();
        _hotkeys = null;
        _scenarios = null;
    }

    public static void SetHotkeysEnabled(bool enabled)
    {
        if (_hotkeys is null) return;

        AppSettings.Current.HotkeysEnabled = enabled;
        IReadOnlyList<HotkeyKind> failed = _hotkeys.SetEnabled(enabled);

        if (failed.Count > 0)
        {
            string names = string.Join(", ", failed.Select(HotkeyInfo.Name));
            ActivityLog.Add("Горячие клавиши", $"Не удалось занять: {names} (используются другой программой)", LogKind.Error);
        }
        else
        {
            ActivityLog.Add("Горячие клавиши", enabled ? "Включены" : "Выключены", LogKind.Info);
        }

        HotkeysStateChanged?.Invoke();
    }
}
