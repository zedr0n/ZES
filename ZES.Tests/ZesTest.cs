using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimpleInjector;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Replay;
using ZES.Tests.Domain;

namespace ZES.Tests
{
    public class ZesTest : Test
    {
        protected ZesTest(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }
        
        protected override IEnumerable<Type> Configs => new List<Type> { typeof(Config) };

        protected override Container CreateContainer(List<Action<Container>> registrations = null)
        {
            var regs = new List<Action<Container>>
            {
                c =>
                {
                    Config.RegisterEvents(c);
                    Config.RegisterAggregates(c);
                    Config.RegisterCommands(c);
                    Config.RegisterQueries(c);
                    Config.RegisterProjections(c);
                },
            };
            if (registrations != null)
                regs.AddRange(registrations);

            return base.CreateContainer(regs);
        }
    }
}