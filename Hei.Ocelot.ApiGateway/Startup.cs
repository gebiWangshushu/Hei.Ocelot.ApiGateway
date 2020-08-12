using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Administration;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Eureka;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hei.Ocelot.ApiGateway
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(env.ContentRootPath, "config"))
                .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: true)
                .AddYamlFile($"appsettings.{env.EnvironmentName}.yml", optional: true, reloadOnChange: true)
                //.AddYamlFile("ocelot.yml")
                //.AddYamlFile($"ocelot.{env.EnvironmentName}.yml", optional: true, reloadOnChange: true);
                .AddJsonFile("ocelot.json")
                .AddJsonFile($"ocelot.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            Configuration = builder.AddConfiguration(configuration).Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            //set Cors Policy
            var corsOrigins = Configuration.GetSection("CorsOrigins")?.Get<string[]>();
            if (corsOrigins?.Length > 0)
            {
                services.AddCors(options =>
                {
                    options.AddDefaultPolicy(builder =>
                    {
                        builder.AllowAnyMethod().AllowAnyHeader().WithOrigins(corsOrigins);
                    });
                });
            }

            var ocelot = services.AddOcelot(Configuration);

            //add Administration
            if (string.IsNullOrEmpty(Configuration.GetValue<string>("Administration:Path"))==false)
            {
                var identityOptions = Configuration.GetSection("Administration:IdentityServer")?.Get<IdentityServerAuthenticationOptions>();
                ocelot.AddAdministration(Configuration.GetValue<string>("Administration:Path"), options =>
                 {
                     options.SupportedTokens = SupportedTokens.Both;
                     options.Authority = identityOptions.Authority;
                     options.ApiName = identityOptions.ApiName;
                     options.RequireHttpsMetadata = identityOptions.RequireHttpsMetadata;
                     options.ApiSecret = identityOptions.ApiSecret;
                 });
            }

            //add ServiceDiscoveryProvider
            if ("Consul".Equals(Configuration["GlobalConfiguration:ServiceDiscoveryProvider:Type"],StringComparison.InvariantCultureIgnoreCase))
            {
                ocelot.AddConsul();
            }
            else if ("Eureka".Equals(Configuration["GlobalConfiguration:ServiceDiscoveryProvider:Type"], StringComparison.InvariantCultureIgnoreCase))
            {
                ocelot.AddEureka();
            }
            else if("Kube".Equals(Configuration["GlobalConfiguration:ServiceDiscoveryProvider:Type"], StringComparison.InvariantCultureIgnoreCase))
            {
                ocelot.AddKubernetesFixed();
            }

            //add route Authentication
            var idProviders = Configuration.GetSection("IdentityProvider")?.Get<List<IdentityServerAuthenticationOptions>>();
            if (idProviders?.Count > 0)
            {
                var authBuilder = services.AddAuthentication();
                foreach (var p in idProviders)
                {
                    authBuilder.AddIdentityServerAuthentication(p.ApiName, options =>
                    {
                        options.Authority = p.Authority;
                        options.ApiName = p.ApiName;
                        options.RequireHttpsMetadata = p.RequireHttpsMetadata;
                        options.SupportedTokens = SupportedTokens.Both;
                        options.ApiSecret = p.ApiSecret;
                    });
                }
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var corsOrigins = Configuration.GetSection("CorsOrigins")?.Get<string[]>();
            if (corsOrigins?.Length > 0)
            {
                app.UseCors();
            }

            var idProviders = Configuration.GetSection("IdentityProvider")?.Get<List<IdentityServerAuthenticationOptions>>();
            if (idProviders?.Count > 0)
            {
                app.UseAuthentication();
            }

            app.UseOcelot().Wait();
        }
    }
}