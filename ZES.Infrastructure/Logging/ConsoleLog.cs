using System;

namespace ZES.Infrastructure.Logging
{
    public class ConsoleLog : ILog
    {
        public long Now => DateTime.UtcNow.Ticks;
        
        public void Write(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}