using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.Execution;
using Xunit;
using Xunit.Abstractions;
using ZES.GraphQL;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using static ZES.Tests.Domain.Config;

namespace ZES.Tests
{
    public class SchemaTests : ZesTest
    {
        public SchemaTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [Fact]
        public void CanCreateSchema()
        {
            var container = CreateContainer();
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var log = container.GetInstance<ILog>();
            
            var schema = schemaProvider.Build().Schema;
            log.Info(schema.ToString());
        }

        [Fact]
        public async void CanSubscribe()
        {
            var container = CreateContainer();
            var log = container.GetInstance<ILog>();
 
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var executor = schemaProvider.Build();
            
            var errorResult = await executor.ExecuteAsync(@"query{ error { message } }") as IReadOnlyQueryResult;
            var responseStream = await executor.ExecuteAsync(@"subscription{ log { message } }") as IResponseStream;

            var message = "Ping!";
            log.Info(message);
            
            using (var cts = new CancellationTokenSource(100))
            {
                await foreach (var dict in responseStream.WithCancellation(cts.Token))
                {
                    var v = dict.Data.SingleOrDefault().Value;
                    Assert.Null(v);
                    break;
                }
            }

            // dynamic dict = eventResult?.Data.SingleOrDefault().Value;

            // subscription stitching is not working yet
            // Assert.Null(dict);
        }

        [Fact]
        public async void CanExecuteQuery()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            
            var executor = schemaProvider.Build();
            var query = generator.Query(new RootInfoQuery("Root"));
            var rootInfoResult = await executor.ExecuteAsync(query) as IReadOnlyQueryResult;
            dynamic rootInfoDict = rootInfoResult?.Data.SingleOrDefault().Value;
            log.Info(rootInfoDict);
            Assert.NotNull(rootInfoDict);
            Assert.NotEqual(0, rootInfoDict["createdAt"]);

            query = generator.Query(new StatsQuery());  
            var statsResult = await executor.ExecuteAsync(query) as IReadOnlyQueryResult;
            dynamic statsDict = statsResult?.Data.SingleOrDefault().Value;
            log.Info(statsDict);
            Assert.NotNull(statsDict);
            Assert.Equal(1, statsDict["numberOfRoots"]);
        }

        [Fact]
        public void CanExecuteMutation()
        {
            var container = CreateContainer();
            var log = container.GetInstance<ILog>(); 
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            
            var executor = schemaProvider.Build();

            var command = generator.Mutation(new CreateRoot("Root"));
            var commandResult = executor.Execute(command);
            foreach (var e in commandResult.Errors)
                log.Error(e.Message, this);
            
            command = generator.Mutation(new CreateRecord("Root"));
            commandResult = executor.Execute(command);
            foreach (var e in commandResult.Errors)
                log.Error(e.Message, this); 

            command = generator.Mutation(new AddRecord("Root", 1));
            commandResult = executor.Execute(command);
            foreach (var e in commandResult.Errors)
                log.Error(e.Message, this);

            var query = generator.Query(new StatsQuery());
            
            var statsResult = executor.Execute(query) as IReadOnlyQueryResult;
            
            dynamic statsDict = statsResult?.Data["statsQuery"];
            log.Info(statsDict);
            Assert.NotNull(statsDict);
            Assert.Equal(1, statsDict["numberOfRoots"]); 
        }

        [Fact]
        public async void CanBranch()
        {
            var container = CreateContainer();
            var log = container.GetInstance<ILog>(); 
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            
            var executor = schemaProvider.Build();

            var command = generator.Mutation(new CreateRoot("Root"));
            var commandResult = executor.Execute(command);
            foreach (var e in commandResult.Errors)
                log.Error(e.Message, this);
 
            await executor.ExecuteAsync(@"mutation { branch( branch : ""test"") } ");

            var result = await executor.ExecuteAsync(@"query { activeBranch }") as IReadOnlyQueryResult;
            var branchId = result?.Data["activeBranch"];
            Assert.Equal("test", branchId);
        }
        
        [Fact]
        public async void CanQueryError()
        {
            var container = CreateContainer();
            
            var log = container.GetInstance<ILog>();
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            var command = generator.Mutation(new CreateRoot("Root"));
            
            var executor = schemaProvider.Build();

            var commandResult = await executor.ExecuteAsync(command);
            foreach (var e in commandResult.Errors)
                log.Error(e.Message, this);
            
            commandResult = await executor.ExecuteAsync(command);
            foreach (var e in commandResult.Errors)
                log.Error(e.Message, this);

            var errorResult = await executor.ExecuteAsync(@"query{ error { message } }") as IReadOnlyQueryResult;
            dynamic messageDict = errorResult?.Data["error"];
            Assert.NotNull(messageDict);
            Assert.Contains("ahead", messageDict["message"]);
        }
    }
}