using System;
using System.Diagnostics;

namespace CNES.Utils
{
    public static class Logger
    {
        // Only write logs in debug mode
        [Conditional("DEBUG")]
        public static void DebugLog(string message)
        {
            Console.WriteLine(message);
        }

        // Error Logs
        public static void ErrorLog(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] " + message);
            Console.ResetColor();
        }
    }
}
