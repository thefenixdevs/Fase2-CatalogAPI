using Microsoft.Extensions.DependencyInjection;
using CatalogAPI.Application.DTOs.Auth.Login;
using CatalogAPI.Application.Events;
using CatalogAPI.Domain.Events;

namespace CatalogAPI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginHandler>();
        services.AddScoped<
            IEventHandler<UserCreatedEvent>,
            UserCreatedEventHandler>();

        return services;
    }
}
