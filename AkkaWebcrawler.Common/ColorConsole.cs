using System;
namespace AkkaWebcrawler.Common
{
    public static class ColorConsole
    {
        private static object _MessageLock = new object();
        public static void WriteLine(string message, ConsoleColor color)
        {
            lock (_MessageLock)
            {
                ConsoleColor previousColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ForegroundColor = previousColor;
            }
        }
    }
}