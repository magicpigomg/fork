using System;
using System.Collections.ObjectModel;
using MultiTool.Models;

namespace MultiTool.Services
{
    /// <summary>Журнал действий для главной страницы. Использовать только из UI-потока.</summary>
    public static class ActivityLog
    {
        private const int MaxEntries = 100;

        public static ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();

        /// <summary>Последнее значение, скопированное по F1.</summary>
        public static string LastValue { get; private set; } = "";

        public static event Action LastValueChanged;

        public static void Add(string source, string message, LogKind kind)
        {
            Entries.Insert(0, new LogEntry(DateTime.Now, source, message, kind));
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(Entries.Count - 1);
        }

        public static void SetLastValue(string value)
        {
            LastValue = value;
            var handler = LastValueChanged;
            if (handler != null) handler();
        }
    }
}
