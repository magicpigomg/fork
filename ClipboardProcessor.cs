namespace MultiTool;

internal static class ClipboardProcessor
{
    // Настройки правила — меняйте здесь.
    private const char Marker = '№';
    private const int TakeAfterMarker = 28; // сколько символов берём после №
    private const int SkipChars = 8;        // «с 9-го символа» => пропускаем 8
    private const int ResultLength = 12;    // сколько символов копируем

    public static bool TryExtract(string text, out string result, out string error)
    {
        result = "";
        int idx = text.IndexOf(Marker);
        if (idx < 0)
        {
            error = $"В буфере нет символа «{Marker}».";
            return false;
        }

        string after = text[(idx + 1)..];
        if (after.Length < TakeAfterMarker)
        {
            error = $"После «{Marker}» только {after.Length} симв., нужно {TakeAfterMarker}.";
            return false;
        }

        string block = after.Substring(0, TakeAfterMarker);
        result = block.Substring(SkipChars, ResultLength);
        error = "";
        return true;
    }
}
