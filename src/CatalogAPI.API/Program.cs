using CatalogAPI.API.Middlewares;
using CatalogAPI.CrossCutting.DependencyInjection;
using CatalogAPI.CrossCutting.Logging;
using CatalogAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.AddSerilogConfiguration();

try
{
    Log.Information("Starting CatalogAPI application");

    // Add services
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration);

    // Skip service validation for EF design-time
    builder.Services.AddLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
    });

    var app = builder.Build();

    // Apply pending migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Log.Information("Ensuring database is created and seeded...");
        try
        {
            // Create database and apply any pending migrations
            await dbContext.Database.MigrateAsync();
            Log.Information("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply migrations. Creating database from current model...");
            try
            {
                // Drop existing database if it exists (for dev only)
                await dbContext.Database.EnsureDeletedAsync();
                // Create new database from current model
                await dbContext.Database.EnsureCreatedAsync();
                Log.Information("Database created successfully from model");
            }
            catch (Exception innerEx)
            {
                Log.Error(innerEx, "Failed to ensure database creation");
            }
        }
    }

    // Configure middleware pipeline
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<AuthenticationMiddleware>();

    app.UseApiConfiguration();

    Log.Information("CatalogAPI application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
