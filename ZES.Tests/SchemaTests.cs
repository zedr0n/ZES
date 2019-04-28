using System;
using HotChocolate.Execution;
using Xunit;
using Xunit.Abstractions;
using ZES.GraphQL;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Commands;
using static ZES.Tests.Domain.Config;
using IError = ZES.Interfaces.IError;

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
            
            var schema = schemaProvider.Generate(
                typeof(Queries),
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
            var rootInfoResult = await executor.ExecuteAsync(@"{ rootInfo( query: { id : ""Root"" } ) { rootId, createdAt, updatedAt }  }") as IReadOnlyQueryResult;
            dynamic rootInfoDict = rootInfoResult?.Data["rootInfo"];
            log.Info(rootInfoDict);
            Assert.NotNull(rootInfoDict);
            Assert.NotEqual(0, rootInfoDict["createdAt"]);

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

        [Fact]
        public async void CanQueryError()
        {
            var container = CreateContainer();
            var errorLog = container.GetInstance<IErrorLog>();
            
            IError error = null;
            errorLog.Errors.Subscribe(e => error = e);
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var executor = schemaProvider.Generate(typeof(Queries), typeof(Mutations));

            await executor.ExecuteAsync(@"mutation { createRoot( command : { target : ""Root"" } ) }"); 
            await executor.ExecuteAsync(@"mutation { createRoot( command : { target : ""Root"" } ) }");

            var errorResult = await executor.ExecuteAsync(@"query{ error { message } }") as IReadOnlyQueryResult;
            dynamic messageDict = errorResult?.Data["error"];
            Assert.NotNull(messageDict);
            Assert.Contains("ahead", messageDict["message"]);
        }
    }
}