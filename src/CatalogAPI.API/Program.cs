using CatalogAPI.API.Middlewares;
using CatalogAPI.Application.UseCases.UserGames.ProcessPayment;
using CatalogAPI.CrossCutting.DependencyInjection;
using CatalogAPI.CrossCutting.Logging;
using CatalogAPI.Domain.Events;
using CatalogAPI.Infrastructure.Data;
using CatalogAPI.Infrastructure.Data.Seeders;
using MassTransit;
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

    // Configure MassTransit with RabbitMQ
    builder.Services.AddMassTransit(x =>
    {
        // Register consumer for PaymentProcessedEvent
        x.AddConsumer<ProcessPaymentEventConsumer>();

        x.UsingRabbitMq((context, cfg) =>
        {
            var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
            var rabbitMqPort = ushort.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
            var rabbitMqUsername = builder.Configuration["RabbitMQ:Username"] ?? "guest";
            var rabbitMqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";
            var rabbitMqVirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

            cfg.Host(rabbitMqHost, rabbitMqPort, rabbitMqVirtualHost, h =>
            {
                h.Username(rabbitMqUsername);
                h.Password(rabbitMqPassword);
            });

            // Configure explicit entity name for OrderPlacedEvent to maintain compatibility
            cfg.Message<OrderPlacedEvent>(m =>
            {
                m.SetEntityName("fcg.order-placed-event");
            });

            // Configure consumer endpoint for PaymentProcessedEvent
            cfg.ReceiveEndpoint("fcg.catalog.payment-processed", e =>
            {
                // Bind to existing exchange created by PaymentsAPI
                e.ConfigureConsumeTopology = false;
                e.Bind("fcg.payment-processed-event", s =>
                {
                    s.RoutingKey = "catalog.payment-processed";
                });
                e.ConfigureConsumer<ProcessPaymentEventConsumer>(context);
            });
        });
    });

    var app = builder.Build();

    // Apply pending migrations on startup with distributed lock to prevent conflicts
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Log.Information("Ensuring database is created and seeded...");
        
        // Use PostgreSQL advisory lock to prevent concurrent migration execution
        const int migrationLockId = 123456789; // Unique lock ID for migrations
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        
        try
        {
            // Try to acquire advisory lock (non-blocking)
            using var lockCommand = connection.CreateCommand();
            lockCommand.CommandText = $"SELECT pg_try_advisory_lock({migrationLockId})";
            var lockResult = await lockCommand.ExecuteScalarAsync();
            var hasLock = lockResult != null && (bool)lockResult;
            
            if (hasLock)
            {
                Log.Information("Acquired migration lock. Applying migrations...");
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
                finally
                {
                    // Release the lock
                    using var unlockCommand = connection.CreateCommand();
                    unlockCommand.CommandText = $"SELECT pg_advisory_unlock({migrationLockId})";
                    await unlockCommand.ExecuteScalarAsync();
                    Log.Information("Migration lock released");
                }
            }
            else
            {
                Log.Information("Migration lock already held by another instance. Skipping migrations (they will be applied by the other instance).");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during migration lock acquisition. Attempting migrations without lock (may cause conflicts if multiple instances start simultaneously).");
            try
            {
                await dbContext.Database.MigrateAsync();
                Log.Information("Database migrations applied successfully (without lock)");
            }
            catch (Exception migrationEx)
            {
                Log.Error(migrationEx, "Failed to apply migrations");
            }
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }

        // Execute seeders after database is ready
        try
        {
            var seederService = scope.ServiceProvider.GetRequiredService<DatabaseSeederService>();
            var seedResults = await seederService.SeedAllAsync(dbContext);
            
            var successCount = seedResults.Count(r => r.Value >= 0);
            var errorCount = seedResults.Count(r => r.Value < 0);
            
            if (errorCount > 0)
            {
                Log.Warning("Seed concluído com {ErrorCount} erro(s). {SuccessCount} seeder(s) executado(s) com sucesso.", 
                    errorCount, successCount);
            }
            else
            {
                Log.Information("Seed concluído com sucesso. {Count} seeder(s) executado(s).", successCount);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao executar seeders. A aplicação continuará sem dados iniciais.");
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
