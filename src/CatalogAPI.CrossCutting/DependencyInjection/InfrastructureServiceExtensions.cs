using CatalogAPI.Domain.Interfaces;
using CatalogAPI.Infrastructure.BackgroundServices;
using CatalogAPI.Infrastructure.Data;
using CatalogAPI.Infrastructure.Data.Seeders;
using CatalogAPI.Infrastructure.Repositories;
using CatalogAPI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace CatalogAPI.CrossCutting.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Add DbContext
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CatalogDatabase")));

        // Add Repositories
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IUserGameRepository, UserGameRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Add Manual Outbox implementation
        services.AddScoped<CatalogAPI.Domain.Interfaces.IOutbox, ManualOutbox>();

        // Add Background Service to process Outbox messages
        services.AddHostedService<OutboxProcessorService>();

        // Add Auth Service with Polly policies
        services.AddHttpClient<IAuthService, HttpAuthService>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Register Seeders
        services.AddScoped<ISeeder, GameSeeder>();
        services.AddScoped<DatabaseSeederService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    }
}
