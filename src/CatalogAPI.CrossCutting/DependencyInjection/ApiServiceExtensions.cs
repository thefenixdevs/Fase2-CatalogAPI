using Asp.Versioning;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogAPI.CrossCutting.DependencyInjection;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Add API Versioning
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        }).AddMvc();

        // Add Controllers
        services.AddControllers();

        // Add Health Checks
        var connectionString = configuration.GetConnectionString("CatalogDatabase");
        var rabbitMqHost = configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitMqPort = configuration["RabbitMQ:Port"] ?? "5672";
        var rabbitMqUsername = configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitMqPassword = configuration["RabbitMQ:Password"] ?? "guest";

        services.AddHealthChecks()
            .AddNpgSql(connectionString!, name: "postgresql")
            .AddRabbitMQ(_ => 
            {
                var factory = new RabbitMQ.Client.ConnectionFactory
                {
                    HostName = rabbitMqHost,
                    Port = int.Parse(rabbitMqPort),
                    UserName = rabbitMqUsername,
                    Password = rabbitMqPassword
                };
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            }, name: "rabbitmq");

        return services;
    }

    public static WebApplication UseApiConfiguration(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapControllers();

        return app;
    }
}
