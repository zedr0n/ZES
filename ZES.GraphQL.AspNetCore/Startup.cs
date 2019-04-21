using HotChocolate;
using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using ZES.Tests.Domain;

namespace ZES.GraphQL.AspNetCore
{
    public class Startup
    {
        private readonly Container _container = new Container();
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var schema = ZES.GraphQL.Startup.WireGraphQl(_container, Config.RegisterAll,
                typeof(Tests.Domain.Schema.Query), typeof(Tests.Domain.Schema.Mutation));
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
