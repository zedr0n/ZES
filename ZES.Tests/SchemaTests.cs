using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate.Execution;
using Xunit;
using ZES.GraphQL;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;

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

        /*[Fact]
        public async Task CanSubscribe()
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
        }*/

        [Fact]
        public async Task CanExecuteQuery()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();
            var generator = container.GetInstance<IGraphQlGenerator>();

            var id = $"{nameof(CanExecuteQuery)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            
            var executor = schemaProvider.Build();
            var query = generator.Query(new RootInfoQuery(id));
            var rootInfoResult = await executor.ExecuteAsync(query) as IReadOnlyQueryResult;
            var rootInfoDict = rootInfoResult?.Data.SingleOrDefault().Value as IReadOnlyDictionary<string, object>;
            log.Info(rootInfoDict);
            Assert.NotNull(rootInfoDict);
            var timeStr = rootInfoDict["createdAt"] as string;
            var time = Time.Parse(timeStr);
            Assert.NotNull(time);
            Assert.NotEqual(default, time.ToInstant());

            query = generator.Query(new StatsQuery());  
            var statsResult = await executor.ExecuteAsync(query) as IReadOnlyQueryResult;
            var statsDict = statsResult?.Data.SingleOrDefault().Value as IReadOnlyDictionary<string, object>;
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

            var id = $"{nameof(CanExecuteMutation)}-Root";
            var command = generator.Mutation(new CreateRoot(id));
            var commandResult = executor.Execute(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this);
            }
            
            command = generator.Mutation(new CreateRecord(id));
            commandResult = executor.Execute(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this); 
            }
            
            command = generator.Mutation(new AddRecord(id, 1));
            commandResult = executor.Execute(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this);
            }
            
            var query = generator.Query(new StatsQuery());
            var statsResult = executor.Execute(query) as IReadOnlyQueryResult;
            
            var statsDict = statsResult?.Data["statsQuery"] as IReadOnlyDictionary<string, object>;
            log.Info(statsDict);
            Assert.NotNull(statsDict);
            Assert.Equal(1, statsDict["numberOfRoots"]);
        }

        [Fact]
        public void CannotCreateRootTwice()
        {
            var container = CreateContainer();
            var log = container.GetInstance<ILog>(); 
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            
            var executor = schemaProvider.Build();

            var id = $"{nameof(CannotCreateRootTwice)}-Root";
            var command = generator.Mutation(new CreateRoot(id));
            var commandResult = executor.Execute(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this);
            }

            commandResult = executor.Execute(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this);
            }
        }
        [Fact]
        public async Task CanReplayLog()
        {
            var container = CreateContainer();
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            
            var executor = schemaProvider.Build();
            var id = $"{nameof(CanReplayLog)}-Root";

            var command = generator.Mutation(new CreateRoot(id));
            await executor.ExecuteAsync(command);
            
            var query = generator.Query(new StatsQuery());
            await executor.ExecuteAsync(query); 
 
            var recordLog = container.GetInstance<IRecordLog>();
            var logFile = $"{nameof(CanReplayLog)}.json";
            await recordLog.Flush(logFile);

            var container2 = CreateContainer(db: 1);
            schemaProvider = container2.GetInstance<ISchemaProvider>();
            recordLog = container2.GetInstance<IRecordLog>();
            var scenario = await recordLog.Load(logFile);
            await schemaProvider.Replay(scenario);

            Assert.True(recordLog.Validate(scenario));
        }

        [Fact]
        public async Task CanBranch()
        {
            var container = CreateContainer();
            var log = container.GetInstance<ILog>(); 
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            
            var executor = schemaProvider.Build();
            var id = $"{nameof(CanBranch)}-Root";

            var command = generator.Mutation(new CreateRoot(id));
            var commandResult = executor.Execute(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this);
            }
 
            await executor.ExecuteAsync(@"mutation { branch( branch : ""test"") } ");

            var result = await executor.ExecuteAsync(@"query { activeBranch }") as IReadOnlyQueryResult;
            Assert.Null(result?.Errors);
            var branchId = result?.Data["activeBranch"];
            Assert.Equal("test", branchId);
        }
        
        [Fact]
        public async Task CanQueryError()
        {
            var container = CreateContainer();
            
            var log = container.GetInstance<ILog>();
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var generator = container.GetInstance<IGraphQlGenerator>();
            var id = $"{nameof(CanQueryError)}-Root";
            var command = generator.Mutation(new CreateRoot(id));
            
            var executor = schemaProvider.Build();

            var commandResult = await executor.ExecuteAsync(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this);
            }

            commandResult = await executor.ExecuteAsync(command);
            if (commandResult.Errors != null)
            {
                foreach (var e in commandResult.Errors)
                    log.Error(e.Message, this);
            }

            var errorResult = await executor.ExecuteAsync(@"query{ error { message } }") as IReadOnlyQueryResult;
            var messageDict = errorResult?.Data["error"] as IReadOnlyDictionary<string, object>;
            Assert.NotNull(messageDict);
            Assert.Contains("ahead", messageDict["message"] as string);
        }
    }
}