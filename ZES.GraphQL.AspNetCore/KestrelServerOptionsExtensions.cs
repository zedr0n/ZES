using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CS01618

namespace ZES.GraphQL.AspNetCore
{
    public static class KestrelServerOptionsExtensions
    {
        public static void ConfigureEndpoints(this KestrelServerOptions options)
        {
            var environment = options.ApplicationServices.GetRequiredService<IHostingEnvironment>();

            var endpoints = new List<EndpointConfiguration>
            {
                new EndpointConfiguration
                {
                    Host = "localhost",
                    Port = 5001,
                    Scheme = "https",
                    StoreName = "My",
                    StoreLocation = "CurrentUser",
                },
                new EndpointConfiguration
                {
                    Host = "localhost",
                    Port = 5000,
                    Scheme = "http",
                },
            };
        
            foreach (var endpoint in endpoints)
            {
                var config = endpoint;
                var port = config.Port ?? (config.Scheme == "https" ? 443 : 80);

                var ipAddresses = new List<IPAddress>();
                if (config.Host == "localhost")
                {
                    ipAddresses.Add(IPAddress.IPv6Loopback);
                    ipAddresses.Add(IPAddress.Loopback);
                }
                else if (IPAddress.TryParse(config.Host, out var address))
                {
                    ipAddresses.Add(address);
                }
                else
                {
                    ipAddresses.Add(IPAddress.IPv6Any);
                }

                foreach (var address in ipAddresses)
                {
                    options.Listen(
                        address, 
                        port,
                        listenOptions =>
                        {
                            if (config.Scheme == "https")
                            {
                                var certificate = LoadCertificate(config, environment);
                                listenOptions.UseHttps(certificate);
                            }
                        });
                }
            }
        }

        private static X509Certificate2 LoadCertificate(EndpointConfiguration config, IHostingEnvironment environment)
        {
            if (config.StoreName != null && config.StoreLocation != null)
            {
                using (var store = new X509Store(config.StoreName, Enum.Parse<StoreLocation>(config.StoreLocation)))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var certificate = store.Certificates.Find(
                        X509FindType.FindBySubjectName,
                        config.Host,
                        validOnly: !environment.IsDevelopment());

                    if (certificate.Count == 0)
                    {
                        throw new InvalidOperationException($"Certificate not found for {config.Host}.");
                    }

                    return certificate[0];
                }
            }

            if (config.FilePath != null && config.Password != null)
            {
                return new X509Certificate2(config.FilePath, config.Password);
            }

            throw new InvalidOperationException("No valid certificate configuration found for the current endpoint.");
        }
        
        private class EndpointConfiguration
        {
            public string Host { get; set; }
            public int? Port { get; set; }
            public string Scheme { get; set; }
            public string StoreName { get; set; }
            public string StoreLocation { get; set; }
            public string FilePath { get; set; }
            public string Password { get; set; }
        }
    }
}