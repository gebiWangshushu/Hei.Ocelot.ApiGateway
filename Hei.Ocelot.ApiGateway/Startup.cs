using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Administration;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System;
using System.IO;

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
                .AddJsonFile("ocelot.json")
                .AddJsonFile($"ocelot.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            Configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var identityOptions = Configuration.GetSection("AddAdministration:IdentityServer")?.Get<IdentityServerAuthenticationOptions>();
            Console.WriteLine($"Authority:{identityOptions.Authority}");
            services.AddOcelot(Configuration)
                    //.AddKubernetes()
                    .AddAdministration(Configuration.GetValue<string>("AddAdministration:Path")?? "/administration", options =>
                    {
                        options.SupportedTokens = SupportedTokens.Both;
                        options.Authority = identityOptions.Authority;
                        options.ApiName = identityOptions.ApiName;
                        options.RequireHttpsMetadata = identityOptions.RequireHttpsMetadata;
                        options.ApiSecret = identityOptions.ApiSecret;
                    });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseOcelot().Wait();
        }
    }
}