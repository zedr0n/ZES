using HotChocolate;
using HotChocolate.Execution;
using Xunit;
using Xunit.Abstractions;
using ZES.GraphQL;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Commands;
using static ZES.Tests.Domain.Config;

namespace ZES.Tests
{
    public class SchemaTests : ZesTest
    {
        public SchemaTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void CanCreateSchema()
        {
            var container = CreateContainer();
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var log = container.GetInstance<ILog>();
            
            var schema = schemaProvider.Generate(typeof(Queries),
            typeof(Mutations)).Schema;
            log.Info(schema.ToString());
        }

        [Fact]
        public async void CanExecuteQuery()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            
            var executor = schemaProvider.Generate(typeof(Queries));
            var createdAtResult = await executor.ExecuteAsync(@"{ createdAt( query: { id : ""Root"" } ) { timestamp }  }") as IReadOnlyQueryResult;
            dynamic createdAtDict = createdAtResult?.Data["createdAt"];
            log.Info(createdAtDict);
            Assert.NotNull(createdAtDict);
            Assert.NotEqual(0, createdAtDict["timestamp"]);

            var statsResult = await executor.ExecuteAsync(@"{ stats( query : {  } ) { numberOfRoots } }") as IReadOnlyQueryResult;
            dynamic statsDict = statsResult?.Data["stats"];
            log.Info(statsDict);
            Assert.NotNull(statsDict);
            Assert.Equal(1, statsDict["numberOfRoots"]);
        }

        [Fact]
        public async void CanExecuteMutation()
        {
            var container = CreateContainer();
            var log = container.GetInstance<ILog>(); 
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            
            var executor = schemaProvider.Generate(typeof(Queries), typeof(Mutations));

            await executor.ExecuteAsync(@"mutation { createRoot( command : { target : ""Root"" } ) }");
            
            var statsResult = executor.Execute(@"{ stats( query : {  } ) { numberOfRoots } }") as IReadOnlyQueryResult;
            dynamic statsDict = statsResult?.Data["stats"];
            log.Info(statsDict);
            Assert.NotNull(statsDict);
            Assert.Equal(1, statsDict["numberOfRoots"]); 
        }
    }
}