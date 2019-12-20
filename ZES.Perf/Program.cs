namespace ZES.Perf
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using SimpleInjector;
    using ZES.Interfaces;
    using ZES.Interfaces.Pipes;
    using ZES.Tests.Domain;
    using ZES.Tests.Domain.Commands;
    using ZES.Tests.Domain.Queries;
    using ZES.Utils;

    public class Program
    {
        public static void Main(string[] args)
        {
            Run().Wait();
        }

        private static Container CreateContainer()
        {
            var container = new Container();
            container.Options.DefaultLifestyle = Lifestyle.Singleton;
            var config = Logging.NLog.Configure();
            Logging.NLog.Enable(config);

            CreateRoot().ComposeApplication(container);
            container.Register<IServiceCollection>(() => new ServiceCollection(), Lifestyle.Singleton);

            Config.RegisterCommands(container);
            Config.RegisterQueries(container);
            Config.RegisterProjections(container);

            container.Verify();
            return container;
        }

        private static async Task Run()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var numRoots = 10000;
            var log = container.GetInstance<ILog>();
            var t = Stopwatch.StartNew();
            var rootId = numRoots;
            while (rootId > 0)
            {
                var command = new CreateRoot($"Root{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }

            var statsQuery = new StatsQuery();
            await bus.QueryUntil(statsQuery, s => s?.NumberOfRoots == numRoots, TimeSpan.FromSeconds(numRoots / 1000));
            log.Info($"Total time : {t.ElapsedMilliseconds}, per root : {(float)t.ElapsedMilliseconds / numRoots}");
        }

        private static CompositionRoot CreateRoot() => new CompositionRoot();
    }
}