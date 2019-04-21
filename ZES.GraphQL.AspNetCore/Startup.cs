using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;

namespace ZES.GraphQL.AspNetCore
{
    public class Startup
    {
        private readonly Container _container = new Container();
        
        private void IntegrateSimpleInjector(IServiceCollection services) {
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            new CompositionRoot().ComposeApplication(_container);
            _container.Register<ISchemaProvider,SchemaProvider>(Lifestyle.Singleton);
            Config.RegisterAll(_container);
            
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            
            services.EnableSimpleInjectorCrossWiring(_container);
            services.UseSimpleInjectorAspNetRequestScoping(_container);

            services.AddSingleton(p => _container.GetService<IBus>());
            _container.Verify();
        }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            IntegrateSimpleInjector(services);
            var schemaProvider = _container.GetInstance<ISchemaProvider>();
            schemaProvider.SetQuery(typeof(Tests.Domain.Schema.Query));
            schemaProvider.SetMutation(typeof(Tests.Domain.Schema.Mutation));
            var schema = schemaProvider.Generate();
            services.AddGraphQL(schema);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseGraphQL();
        }
    }
}
