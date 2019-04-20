using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    public class QueryType<TQuery,TResult> : ObjectType<TQuery>
        where TQuery : IQuery<TResult> 
    {
        protected override void Configure(IObjectTypeDescriptor<TQuery> descriptor)
        {
            //descriptor.Field(t => t.Result).Resolver(async ctx => 
            //    await ((IBus) ctx.ContextData["Bus"])?.QueryAsync(ctx.Parent<TQuery>()));
            base.Configure(descriptor);
        }
    }
    
    public class SchemaProvider : ISchemaProvider
    {
        private readonly IBus _bus;
        private readonly IEnumerable<ITypeProvider<IQuery>> _queries;

        public SchemaProvider(IEnumerable<ITypeProvider<IQuery>> queries, IBus bus)
        {
            _queries = queries;
            _bus = bus;
        }

        public ISchema Generate()
        {
            var schema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                c.Use(next => context =>
                {
                    context.ContextData["Bus"] = _bus;
                    return next.Invoke(context);
                });
                foreach (var q in _queries.Where(x => x.ClrType != null))
                {
                    var result = q.ClrType.GetInterfaces().SingleOrDefault(g => g.IsGenericType)?.GetGenericArguments().SingleOrDefault();  
                    c.RegisterType(typeof(QueryType<,>).MakeGenericType(q.ClrType, result)); 
                }



            });
            return schema;
        }
    }
}