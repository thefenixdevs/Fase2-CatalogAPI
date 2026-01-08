using CatalogAPI.Application.Commands;
using CatalogAPI.Application.DTOs;
using CatalogAPI.Application.Mappings;
using CatalogAPI.Application.Queries;
using FluentValidation;
using Mapster;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CatalogAPI.CrossCutting.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var applicationAssembly = Assembly.Load("CatalogAPI.Application");

        // Add Mediator core service as singleton
        services.AddSingleton<Mediator.Mediator>();
        
        // Register handlers as Scoped to avoid DI lifetime issues with DbContext
        services.AddScoped<ICommandHandler<PurchaseGameCommand, Guid>, PurchaseGameCommandHandler>();
        services.AddScoped<IQueryHandler<GetGamesQuery, PaginatedResultDto<GameDto>>, GetGamesQueryHandler>();

        // Add FluentValidation
        services.AddValidatorsFromAssembly(applicationAssembly);

        // Configure Mapster
        MappingConfig.RegisterMappings();
        TypeAdapterConfig.GlobalSettings.Scan(applicationAssembly);

        return services;
    }
}
