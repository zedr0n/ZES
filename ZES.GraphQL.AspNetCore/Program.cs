using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace ZES.GraphQL.AspNetCore
{
    /// <summary>
    /// Base class
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">Arguments</param>
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// Web host builder
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>Modified web host builder</returns>
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseKestrel(options => options.ConfigureEndpoints())
                .UseUrls("http://localhost:5000", "https://localhost:5001");
    }
}
