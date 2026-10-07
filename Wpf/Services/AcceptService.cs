using System.Globalization;
using System.Text.RegularExpressions;
using MultiTool.Models;

namespace MultiTool.Services;

/// <summary>Поля, вытащенные из документа Accept.</summary>
public sealed record AcceptFields(
    string TitleLine, string PayerAccount, string DateAccept,
    string NumberContract, string YnpBen, string DateDoc, string NumberAccept);

/// <summary>Логика бывшей кнопки button8 → Accept.Create_Accept().</summary>
public static class AcceptService
{
    /// <summary>Разбирает документ по номерам строк из настроек.</summary>
    public static bool TryParse(string text, AppSettings s, out AcceptFields fields, out string error)
    {
        fields = null!;
        error = "";
        var lines = ReadLines(text);

        try
        {
            fields = new AcceptFields(
                TitleLine: s.AcceptTitlePrefix + Line(lines, s.AcceptTitleLine, "заголовок"),
                PayerAccount: Cut(lines, s.AcceptPayerLine, s.AcceptPayerSkip, s.AcceptPayerLength, "счёт клиента"),
                DateAccept: Cut(lines, s.AcceptDateAcceptLine, s.AcceptDateAcceptSkip, null, "дата акцепта"),
                NumberContract: Cut(lines, s.AcceptContractLine, s.AcceptContractSkip, null, "номер договора"),
                YnpBen: Cut(lines, s.AcceptYnpLine, s.AcceptYnpSkip, null, "УНП бенефициара"),
                DateDoc: Cut(lines, s.AcceptDateDocLine, s.AcceptDateDocSkip, null, "дата договора"),
                NumberAccept: Cut(lines, s.AcceptNumberLine, s.AcceptNumberSkip, null, "номер акцепта").Trim());
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Приводит даты к нужному формату и подставляет поля в шаблон макроса.</summary>
    public static bool TryBuildMacro(AcceptFields f, AppSettings s, out string macro, out string error)
    {
        macro = "";
        if (!TryConvertDate(f.DateAccept, s, out string dateAccept))
        {
            error = $"дата акцепта «{f.DateAccept}» не соответствует формату {s.AcceptInputDateFormat}";
            return false;
        }
        if (!TryConvertDate(f.DateDoc, s, out string dateDoc))
        {
            error = $"дата договора «{f.DateDoc}» не соответствует формату {s.AcceptInputDateFormat}";
            return false;
        }

        string text = s.AcceptTemplate
            .Replace("{PayerAccount}", f.PayerAccount)
            .Replace("{DateAccept}", dateAccept)
            .Replace("{NumberContract}", f.NumberContract)
            .Replace("{YnpBen}", f.YnpBen)
            .Replace("{DateDoc}", dateDoc)
            .Replace("{NumberAccept}", f.NumberAccept);

        // Переводы строк — CRLF, как в исходной программе (без лишнего перевода в конце).
        macro = Regex.Replace(text, @"\r\n|\r|\n", "\r\n");
        error = "";
        return true;
    }

    private static bool TryConvertDate(string value, AppSettings s, out string result)
    {
        bool ok = DateTime.TryParseExact(value, s.AcceptInputDateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out DateTime date);
        result = ok ? date.ToString(s.AcceptOutputDateFormat, CultureInfo.InvariantCulture) : "";
        return ok;
    }

    private static List<string> ReadLines(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines;
    }

    private static string Line(List<string> lines, int index, string name)
    {
        if (index < 0 || index >= lines.Count)
            throw new FormatException($"нет строки {index} ({name}): в документе всего {lines.Count} строк");
        return lines[index];
    }

    private static string Cut(List<string> lines, int index, int skip, int? length, string name)
    {
        string line = Line(lines, index, name);
        skip = Math.Max(0, skip);
        int take = length ?? line.Length - skip;
        if (skip + take > line.Length || take < 0)
            throw new FormatException($"строка {index} ({name}) слишком короткая: {line.Length} симв., нужно {skip + Math.Max(0, take)}");
        return line.Substring(skip, take);
    }
}
