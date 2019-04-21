using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    public static class Startup
    {
        public static ISchema WireGraphQl(Container container, Action<Container> config,
            Type queryType, Type mutationType)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            new CompositionRoot().ComposeApplication(container);
            container.Register<ISchemaProvider,SchemaProvider>(Lifestyle.Singleton);
            config(container);
            
            container.Verify();

            var schemaProvider = container.GetInstance<ISchemaProvider>();
            schemaProvider.SetQuery(queryType);
            schemaProvider.SetMutation(mutationType);

            var schema = schemaProvider.Generate();
            return schema;
        }
    }
    
    public class SchemaProvider : ISchemaProvider
    {
        private readonly IBus _bus;
        private Type _queryType;
        private Type _mutationType;

        public SchemaProvider(IBus bus)
        {
            _bus = bus;
        }

        public void SetQuery(Type queryType)
        {
            _queryType = queryType;
        }

        public void SetMutation(Type mutationType)
        {
            _mutationType = mutationType;
        }

        public ISchema Generate()
        {
            var schema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                c.Use(next => async context =>
                {
                    var fields = _queryType?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    var field = fields?.SingleOrDefault(x =>
                        string.Compare(x.Name, context.Field.Name, StringComparison.OrdinalIgnoreCase) == 0);
                    if (field != null)
                    {
                        var queryType = field.GetParameters().FirstOrDefault();
                        dynamic query = context.Argument<object>(queryType.Name);
                        context.Result = await _bus.QueryAsync(query);
                        return;
                    }

                    var commandFields = _mutationType?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    field = commandFields?.SingleOrDefault(x =>
                        string.Compare(x.Name, context.Field.Name, StringComparison.OrdinalIgnoreCase) == 0);

                    if (field != null)
                    {
                        var commandType = field.GetParameters().FirstOrDefault();
                        dynamic command = context.Argument<object>(commandType.Name);
                        await await _bus.CommandAsync(command);
                        context.Result = true;
                        return;
                    }

                    await next.Invoke(context);
                });
                /*foreach (var q in _queries.Select(x => x.ClrType).Where(x => x != null))
                {
                    var result = q.GetInterfaces().SingleOrDefault(g => g.IsGenericType)?.GetGenericArguments().SingleOrDefault();  
                    if(result != null)
                        c.RegisterType(typeof(ObjectType<>).MakeGenericType(result)); 
                }*/

                if (_queryType != null)
                    c.RegisterQueryType(typeof(ObjectType<>).MakeGenericType(_queryType)); 
                
                if(_mutationType != null)
                    c.RegisterMutationType(typeof(ObjectType<>).MakeGenericType(_mutationType));
            });
            return schema;
        }
    }
}