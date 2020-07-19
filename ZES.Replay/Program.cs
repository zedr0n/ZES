using System;
using System.IO;
using System.Threading.Tasks;
using ZES.GraphQL;
using ZES.Tests.Domain;

#pragma warning disable 1591

namespace ZES.Replay
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var player = new Replayer();
            player.UseGraphQl(typeof(Config));
            if (args.Length != 1)
                Console.WriteLine("ZES.Replayer log.json");

            var logFile = args[0];
            var task = Task.Run(() => player.Replay(logFile));

            var result = task.Result;
            Console.WriteLine($"{result.Result}, replay took {result.Elapsed} ms");
            
            if (!result.Result)
                Console.WriteLine($"Mismatch in output : {result.Difference}");
                
            var name = logFile.Split('.')[0];
            using (var sw = new StreamWriter($"{name}_output.json"))
            {
               sw.WriteLine(result.Output); 
            }
        }
    }
}