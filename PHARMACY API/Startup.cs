using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using SendGrid;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Configuration;
using System.Linq;
using static Org.BouncyCastle.Math.EC.ECCurve;


namespace PHARMACY_API
{
    public class Startup
    {
        private readonly IConfiguration _config;

        public Startup(IConfiguration config)
        {
            _config = config;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            // Add Swagger
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
                options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
            });

            var emailConfig = _config.GetSection("EmailConfiguration").Get<Model.Models.EmailConfiguration>();
            services.AddSingleton(emailConfig);
            //services.AddScoped<IEmailSender, SenderEmail>();

            // Add other services here...
        }

        public void Configure(IApplicationBuilder app)
        {
            // Use Swagger
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
                options.RoutePrefix = string.Empty;
            });

            // Use other middleware here...
        }
    }
}
