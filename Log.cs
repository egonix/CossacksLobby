using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

enum LogLevel
{
    Debug,
    Info,
    Critical,
}

static class Log
{
    public static LogLevel Level;

    public static void Debug(string message) { Append(LogLevel.Debug, message); }
    public static void Debug(string message, byte[] buffer, int count) { Append(LogLevel.Debug, message, buffer, count); }

    public static void Info(string message) { Append(LogLevel.Debug, message); }
    public static void Info(string message, byte[] buffer, int count) { Append(LogLevel.Debug, message, buffer, count); }

    public static void Critical(string message) { Append(LogLevel.Debug, message); }
    public static void Critical(string message, byte[] buffer, int count) { Append(LogLevel.Debug, message, buffer, count); }

    public static void Append(LogLevel level, string message)
    {
        if (level < Level) return;
        DateTime dateTime = DateTime.Now;
        ToConsole(dateTime, message);
    }

    public static void Append(LogLevel level, string message, byte[] buffer, int count)
    {
        StringBuilder newMessage = new StringBuilder(message.Length + count * 2);
        newMessage.Append(message);
        for (int i = 0; i < count; i++)
        {
            if (i % 16 == 0)
            {
                newMessage.AppendLine();
                newMessage.Append('\t');
            }
            newMessage.AppendFormat("{0:x2} ", buffer[i]);
        }
        Append(level, newMessage.ToString());
    }

    private static void ToConsole(DateTime dateTime, string message)
    {
        Console.Error.WriteLine($"[{dateTime:hh:mm:ss.fff}] {message}");
    }
}
