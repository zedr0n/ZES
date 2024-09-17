using System;
using System.Collections.Generic;
using SimpleInjector;
using Xunit.Abstractions;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Persistence.Redis;
using ZES.TestBase;
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

        protected override Container CreateContainer(List<Action<Container>> registrations = null, bool resetDb = false, int db = 0)
        {
            var regs = new List<Action<Container>>
            {
                Config.RegisterAllButSagas,
                Config.RegisterOverrides
            };
            if (registrations != null)
                regs.AddRange(registrations);

            return base.CreateContainer(regs, resetDb, db);
        }
    }
}