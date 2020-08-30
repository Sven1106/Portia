using System;
namespace AkkaWebcrawler
{
    public static class ColorConsole
    {
        private static object _MessageLock = new object();
        public static void WriteLine(string message, ConsoleColor color)
        {
            lock (_MessageLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
        }
    }
}