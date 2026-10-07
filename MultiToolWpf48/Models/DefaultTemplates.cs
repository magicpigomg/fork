namespace MultiTool.Models
{
    /// <summary>Шаблоны файлов по умолчанию. {Date} заменяется на дату в заданном формате.</summary>
    public static class DefaultTemplates
    {
        public const string DateFile = "Description =\n\"{Date}";

        /// <summary>Макрос !accept.mac. Подстановки: {PayerAccount} {DateAccept} {NumberContract} {YnpBen} {DateDoc} {NumberAccept}.</summary>
        public const string AcceptMacro =
            "Description =\n" +
            "[home]\n" +
            "\"Y7Y\n" +
            "[enter]\n" +
            "\"{PayerAccount}\n" +
            "\"{DateAccept}\n" +
            "\"{NumberContract}\n" +
            "[tab field]\n" +
            "\"{YnpBen}\n" +
            "\"{DateDoc}\n" +
            "\"{NumberAccept}\n" +
            "[enter]\n" +
            "[down]\n" +
            "[down]\n" +
            "[left]\n" +
            "[left]";

        public const string PriostanovlenieScript =
            "Description =\n" +
            "[home]\n" +
            "\"YIO\n" +
            "[enter]\n" +
            "[edit-paste]\n" +
            "[enter]\n" +
            "[pf6]\n" +
            "wait 5000 msec\n" +
            "[pf12]\n" +
            "\n" +
            "\"1\n" +
            "[enter]\n" +
            "[pf14]\n" +
            "[enter]\n" +
            "wait 7000 msec\n" +
            "[pf3]\n" +
            "[pf3]\n" +
            "[pf3]\n" +
            "\"UWB\n" +
            "[enter]\n" +
            "[tab field]\n" +
            "[edit-paste]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "\"a\n" +
            "[enter]\n" +
            "[down]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "\"2\n" +
            "[enter]\n" +
            "\"{Date}{Date}\n" +
            "[enter]\n" +
            "[pf12]\n" +
            "\"YIO\n" +
            "[enter]\n" +
            "[edit-paste]\n" +
            "[enter]\n" +
            "\"2222222222222\n" +
            "[enter]\n" +
            "[pf12]\n" +
            "[pf12]\n" +
            "\"UWB\n" +
            "[enter]\n" +
            "[tab field]\n" +
            "[edit-paste]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "[tab field]\n" +
            "\"p\n" +
            "[enter]\n" +
            "[down]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "\"4\n" +
            "[enter]\n" +
            "\"{Date}{Date}\n" +
            "[pf5]\n" +
            "[enter]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[left]\n" +
            "[down]\n" +
            "\"a\n" +
            "[enter]\n" +
            "wait 3000 msec\n" +
            "[pf12]";
    }
}
