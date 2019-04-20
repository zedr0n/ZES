using HotChocolate;
using HotChocolate.Execution;
using Xunit;
using Xunit.Abstractions;
using ZES.GraphQL;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Commands;

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
            
            var schema = schemaProvider.Generate();
            log.Info(schema.ToString());
        }

        public async void CanExecuteQuery()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            
            var command = new CreateRoot("Root");
            await await bus.CommandAsync(command);
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            
            var schema = schemaProvider.Generate();
            var executor = schema.MakeExecutable();
            //executor.Execute();
        }
    }
}