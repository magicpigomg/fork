using System;

namespace MultiTool.Models
{
    public enum LogKind { Info, Success, Error }

    public sealed class LogEntry
    {
        public LogEntry(DateTime time, string source, string message, LogKind kind)
        {
            Time = time;
            Source = source;
            Message = message;
            Kind = kind;
        }

        public DateTime Time { get; private set; }
        public string Source { get; private set; }
        public string Message { get; private set; }
        public LogKind Kind { get; private set; }
    }
}
