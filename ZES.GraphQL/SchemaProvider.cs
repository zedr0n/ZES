using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HotChocolate;
using HotChocolate.Types;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{

    
    public class SchemaProvider : ISchemaProvider
    {
        private readonly IBus _bus;
        private readonly IEnumerable<ITypeProvider<IQuery>> _queries;
        private Type _queryType;

        public SchemaProvider(IEnumerable<ITypeProvider<IQuery>> queries, IBus bus)
        {
            _queries = queries;
            _bus = bus;
        }

        public void SetQuery(Type queryType)
        {
            _queryType = queryType;
        }

        public ISchema Generate()
        {
            var schema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                c.Use(next => async context =>
                {
                    var fields = _queryType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    var field = fields.SingleOrDefault(x =>
                        string.Compare(x.Name, context.Field.Name, StringComparison.OrdinalIgnoreCase) == 0);
                    if (field != null)
                    {
                        var queryType = field.GetParameters().FirstOrDefault();
                        dynamic query = context.Argument<object>(queryType.Name);
                        context.Result = await _bus.QueryAsync(query);
                        return;
                    }

                    await next.Invoke(context);
                });
                foreach (var q in _queries.Select(x => x.ClrType).Where(x => x != null))
                {
                    var result = q.GetInterfaces().SingleOrDefault(g => g.IsGenericType)?.GetGenericArguments().SingleOrDefault();  
                    if(result != null)
                        c.RegisterType(typeof(ObjectType<>).MakeGenericType(result)); 
                }

                if (_queryType != null)
                    c.RegisterQueryType(typeof(ObjectType<>).MakeGenericType(_queryType)); 
            });
            return schema;
        }
    }
}