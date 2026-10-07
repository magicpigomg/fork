using System.Text;
using System.Text.RegularExpressions;
using MultiTool.Models;

namespace MultiTool.Services;

public readonly record struct ExtractResult(bool Success, string Kind, string Value, string Error);

/// <summary>Определяет тип документа по тексту из буфера и достаёт нужный фрагмент (правила — в настройках).</summary>
public static class ClipboardProcessor
{
    private const char Marker = '№';

    public static ExtractResult Extract(string text, AppSettings s)
    {
        if (Matches(text, s.RevocationKeyword))
            return Wrap("Отзыв документа", ExtractIban(text, s));

        if (Matches(text, s.StatementKeyword))
        {
            if (Matches(text, s.SuspendedKeyword))
                return Wrap("Заявление (приостановление)",
                    ExtractAfterMarker(text, s, s.SuspendedOccurrence, s.SuspendedSkip, s.SuspendedLength));

            return Wrap("Заявление",
                ExtractAfterMarker(text, s, s.StatementOccurrence, s.StatementSkip, s.StatementLength));
        }

        // Проверяется последним, чтобы не менять поведение «Заявления».
        if (Matches(text, s.OrderKeyword))
            return Wrap("Распоряжение",
                ExtractAfterMarker(text, s, s.OrderOccurrence, s.OrderSkip, s.OrderLength));

        return new ExtractResult(false, "",  "",
            $"Тип документа не определён (нет «{s.RevocationKeyword}» / «{s.StatementKeyword}» / «{s.OrderKeyword}»)");
    }

    private static ExtractResult Wrap(string kind, (string? Value, string? Error) r) =>
        r.Value is null ? new ExtractResult(false, kind, "", r.Error ?? "") : new ExtractResult(true, kind, r.Value, "");

    private static (string? Value, string? Error) ExtractIban(string text, AppSettings s)
    {
        Match label = BuildRegex(s.PayerAccountLabel).Match(text);
        if (!label.Success)
            return (null, $"Не найден текст «{s.PayerAccountLabel}»");

        int by = text.IndexOf(s.IbanPrefix, label.Index + label.Length, StringComparison.Ordinal);
        if (by < 0)
            return (null, $"После «{s.PayerAccountLabel}» нет «{s.IbanPrefix}»");

        int skip = Math.Max(0, s.IbanSkip), length = Math.Max(0, s.IbanLength);
        if (text.Length - by < skip + length)
            return (null, $"После «{s.IbanPrefix}» слишком мало символов");

        return (text.Substring(by + skip, length), null);
    }

    private static (string? Value, string? Error) ExtractAfterMarker(
        string text, AppSettings s, int occurrence, int skip, int length)
    {
        occurrence = Math.Max(1, occurrence);
        skip = Math.Max(0, skip);
        length = Math.Max(0, length);

        int idx = -1;
        for (int found = 0; found < occurrence; found++)
        {
            idx = text.IndexOf(Marker, idx + 1);
            if (idx < 0)
            {
                return (null, found == 0
                    ? $"В буфере нет символа «{Marker}»"
                    : $"В буфере только {found} симв. «{Marker}», а нужен №{occurrence}");
            }
        }

        string after = text[(idx + 1)..];
        if (after.Length < s.TakeAfterMarker)
            return (null, $"После «{Marker}» только {after.Length} симв., нужно {s.TakeAfterMarker}");

        string block = after[..s.TakeAfterMarker];
        if (block.Length < skip + length)
            return (null, $"В {s.TakeAfterMarker} символах после «{Marker}» не хватает места: пропуск {skip} + длина {length}");

        return (block.Substring(skip, length), null);
    }

    private static bool Matches(string text, string keyword) =>
        !string.IsNullOrWhiteSpace(keyword) && BuildRegex(keyword).IsMatch(text);

    /// <summary>Ключевая фраза → regex: без учёта регистра, пробелы = любые пробелы/переносы, е = ё.</summary>
    private static Regex BuildRegex(string keyword)
    {
        var sb = new StringBuilder();
        foreach (char c in keyword.Trim())
        {
            if (char.IsWhiteSpace(c)) { if (sb.Length == 0 || sb[^1] != '+') sb.Append(@"\s+"); }
            else if (c is 'е' or 'ё' or 'Е' or 'Ё') sb.Append("[её]");
            else sb.Append(Regex.Escape(c.ToString()));
        }
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
