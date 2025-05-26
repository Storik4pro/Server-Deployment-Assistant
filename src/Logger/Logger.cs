using ServerDeploymentAssistant.src.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServerDeploymentAssistant
{
    /// <summary>
    /// Basic logger class. WriteColor original author is Walter Stabosz.
    /// https://stackoverflow.com/a/60492990
    /// </summary>
    internal class Logger
    {
        static void WriteColor(string message, ConsoleColor color)
        {
            var pieces = Regex.Split(message, @"(\<[^\>]*\>)");

            foreach (var rawPiece in pieces)
            {
                bool isTag = rawPiece.StartsWith("<") && rawPiece.EndsWith(">");
                string piece = isTag
                    ? rawPiece.Substring(1, rawPiece.Length - 2)
                    : rawPiece;

                ConsoleColor normalColor = isTag
                    ? color
                    : Console.ForegroundColor;

                foreach (char ch in piece)
                {
                    if (ch == '[' || ch == ']')
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(ch);
                    }
                    else
                    {
                        Console.ForegroundColor = normalColor;
                        Console.Write(ch);
                    }
                }

                Console.ResetColor();
            }
        }

        private static readonly object _logLock = new object();

        public static void CreateLog(string message, ConsoleColor consoleHighlightColor = ConsoleColor.White)
        {
            lock (_logLock)
            {
                WriteColor($"<{DateTime.Now}> ", ConsoleColor.DarkGray);
                WriteColor($"[<INFO>] ", ConsoleColor.Blue);
                WriteColor($"{message}", consoleHighlightColor);
                Console.WriteLine();
            }
        }
        public static void CreateError(string message, ConsoleColor consoleHighlightColor = ConsoleColor.White)
        {
            lock (_logLock)
            {
                WriteColor($"<{DateTime.Now}> ", ConsoleColor.DarkGray);
                WriteColor($"[<CRIT>] ", ConsoleColor.Red);
                WriteColor($"{message}", consoleHighlightColor);
                Console.WriteLine();
            }
        }
        public static void CreateWarning(string message, ConsoleColor consoleHighlightColor = ConsoleColor.White)
        {
            lock (_logLock)
            {
                WriteColor($"<{DateTime.Now}> ", ConsoleColor.DarkGray);
                WriteColor($"[<WARN>] ", ConsoleColor.Yellow);
                WriteColor($"{message}", consoleHighlightColor);
                Console.WriteLine();
            }
        }
        public static void RequestAnyButton()
        {
            lock (_logLock)
            {
                if (StateHelper.Instance.enablePressButtonRequest)
                {
                    Console.WriteLine("Press any button to continue ...");
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("PressButtonRequest are disabled");
                }
            }
        }
    }
}
