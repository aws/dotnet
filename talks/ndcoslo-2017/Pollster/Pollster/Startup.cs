using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Amazon.DynamoDBv2;
using System.IO;

namespace Pollster
{
    public class Startup
    {
        IHostingEnvironment _env;

        public Startup(IHostingEnvironment env)
        {
            this._env = env;
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
            services.AddAWSService<IAmazonDynamoDB>();
            services.AddSingleton<Pollster.Models.PollManager>();

            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var logger = loggerFactory.CreateLogger<Startup>();


            // The index.html file is an embedded file instead of a static file so that we can modify the <base href="/Prod/" /> tag
            // to the root of the application. Right now the logic is simple just defaulting to "/" for development
            // and /Prod/ for production. A better version would be either pass the base path as an environment variable or
            // determine the resource path by the underlying Amazon.Lambda.APIGatewayEvents.APIGatewayCustomAuthorizerRequest
            // that can be found in the context.Items collection when running in Lambda.
            app.Use(async (context, next) => {

                logger.LogInformation($"Request: {context.Request.Path}");

                await next();

                if (context.Response.StatusCode == 404 && !Path.HasExtension(context.Request.Path.Value))
                {
                    using (var reader = new StreamReader(typeof(Startup).GetTypeInfo().Assembly.GetManifestResourceStream("Pollster.index.html")))
                    {
                        logger.LogInformation("Returning starting index page");

                        var content = reader.ReadToEnd();
                        if(env.IsDevelopment())
                        {
                            logger.LogInformation("Switching app base to \"/\"");
                            content = content.Replace("/Prod/", "/");
                        }

                        context.Response.StatusCode = 200;
                        context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";

                        var buffer = System.Text.UTF8Encoding.UTF8.GetBytes(content);
                        await context.Response.Body.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
            });

            app.UseStaticFiles();
            app.UseMvc();
        }
    }
}
