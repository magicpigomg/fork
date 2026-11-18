namespace MultiTool.Models
{
    /// <summary>
    /// Своё правило для заявления: по ключевым фразам определяет вид заявления и говорит, какой по счёту «№» взять
    /// и сколько символов пропустить и взять после него. Список правил редактируется в настройках («Приостановления»).
    /// </summary>
    public sealed class StatementRule
    {
        /// <summary>Правило включено; выключенное пропускается, но остаётся в списке.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Название для журнала (необязательно); если пусто — в журнале ключевая фраза.</summary>
        public string Name { get; set; } = "";

        /// <summary>Ключевая фраза: без неё правило не срабатывает. Регистр, лишние пробелы и «е/ё» не важны.</summary>
        public string Keyword { get; set; } = "";

        /// <summary>Доп. фраза (необязательно): если задана, она тоже должна быть в тексте.</summary>
        public string AltKeyword { get; set; } = "";

        /// <summary>Какой по счёту «№» брать (1 — первый).</summary>
        public int Occurrence { get; set; } = 2;

        /// <summary>Сколько символов пропустить после «№».</summary>
        public int Skip { get; set; } = 8;

        /// <summary>Сколько символов взять.</summary>
        public int Length { get; set; } = 13;

        /// <summary>Применять общее правило «три №» (если «№» три и больше и правило берёт второй, берётся указанный в общих настройках).</summary>
        public bool ThreeMarkersRule { get; set; } = true;
    }
}
