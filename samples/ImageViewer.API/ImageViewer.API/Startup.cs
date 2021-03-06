using System;
using System.Linq;
using ImageViewer.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ImageViewer.API
{
    public class Startup
    {
        private const string CorsPolicyName = "CorsPolicy";
        public const string AppS3BucketKey = "AppS3Bucket";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static IConfiguration Configuration { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            string[] corsOrigins = {
                Configuration.GetSection("WebappRedirectUrl").Value,
                Configuration.GetSection("AdditionalCorsOrigins").Value,
            };

            services.AddCors(options =>
            {
                options.AddPolicy(
                    CorsPolicyName,
                    builder => builder.WithOrigins(corsOrigins.Where(co => !string.IsNullOrEmpty(co)).ToArray())
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .WithExposedHeaders("Content-Disposition"));
            });

            // Add S3 to the ASP.NET Core dependency injection framework.
            services.AddAWSService<Amazon.S3.IAmazonS3>();

            //services.AddTransient<IImagesRepository>(c => new ImagesDynamoDbRepository(Configuration["DynamoDbImagesTable"]));

            services.AddTransient<IImagesRepository>(c => new ImagesAuroraRepository(Configuration["AuroraArn"], Configuration["AuroraSecretArn"], Configuration["DatabaseName"]));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (string.Equals(env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseRouting();

            app.UseCors(CorsPolicyName);
            app.UseHttpsRedirection();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
