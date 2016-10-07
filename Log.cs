using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

static class Log
{
    public static void Debug(string message)
    {
        ToConsole(DateTime.Now, message);
    }

    public static void Debug(string message, byte[] buffer, int count)
    {
        int offset = 0;
        count += 14;

        StringBuilder sb = new StringBuilder(message.Length + count * 2);
        sb.AppendLine(message);
        for (int i = 0; i < offset + count; i++)
            sb.AppendFormat("{0:x2} ", buffer[offset + i]);

        ToConsole(DateTime.Now, sb.ToString());
    }

    public static void Info(string message)
    {
        ToConsole(DateTime.Now, message);
    }
    public static void Critical(string message)
    {
        ToConsole(DateTime.Now, message);
    }

    private static void ToConsole(DateTime dateTime, string message)
    {
        Console.Error.WriteLine($"[{dateTime:hh:mm:ss.fff}] {message}");
    }
}
