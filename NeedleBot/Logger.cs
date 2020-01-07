using System;

namespace NeedleBot
{
    public static class Logger
    {
        public static LogLevel LogLevel { get; set; } = LogLevel.Main;

        public static void Write(string message, LogLevel level = LogLevel.Main, ConsoleColor? foregroundColor = null)
        {
            if (LogLevel < level)
                return;

            if (foregroundColor == null)
            {
                Console.WriteLine(message);
            }
            else
            {
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = foregroundColor.Value;
                Console.WriteLine(message);
                Console.ForegroundColor = prevColor;
            }
        }
        public static void WriteMain(string message, ConsoleColor? foregroundColor = null)
        {
            Write(message, LogLevel.Main, foregroundColor);
        }
        public static void WriteExtra(string message, ConsoleColor? foregroundColor = null)
        {
            Write(message, LogLevel.Extra, foregroundColor);
        }
        public static void WriteDebug(string message, ConsoleColor? foregroundColor = null)
        {
            Write(message, LogLevel.Debug, foregroundColor);
        }
    }

    public enum LogLevel
    {
        Main,
        Extra,
        Debug
    }
}
