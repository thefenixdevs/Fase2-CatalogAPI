using CatalogAPI.Infrastructure.Services;

namespace CatalogAPI.API.Middlewares;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        // Skip authentication for health check and swagger endpoints
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        if (path.Contains("/health") || path.Contains("/swagger"))
        {
            await _next(context);
            return;
        }

        // Check if route requires authentication (POST requests)
        if (context.Request.Method == HttpMethods.Post && path.Contains("/purchase"))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Authorization header missing or invalid" });
                return;
            }

            var token = authHeader["Bearer ".Length..].Trim();

            try
            {
                var userContext = await authService.ValidateTokenAsync(token, context.RequestAborted);
                
                if (userContext == null)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { message = "Invalid or expired token" });
                    return;
                }

                // Store user context for use in controllers
                context.Items["User"] = userContext;
                
                _logger.LogInformation("User authenticated: {UserId}, {Email}", userContext.UserId, userContext.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { message = "Authentication service unavailable" });
                return;
            }
        }

        await _next(context);
    }
}
