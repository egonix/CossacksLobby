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
