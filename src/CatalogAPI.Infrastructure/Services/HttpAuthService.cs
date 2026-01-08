using System.Net.Http.Headers;
using System.Text.Json;
using CatalogAPI.Application.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CatalogAPI.Infrastructure.Services;

public interface IAuthService
{
    Task<UserContextDto?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

public class HttpAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpAuthService> _logger;
    private readonly string _baseUrl;

    public HttpAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<HttpAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["AuthService:BaseUrl"] ?? "http://localhost:3000";
    }

    public async Task<UserContextDto?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/auth/validate");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Auth service returned status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var userContext = JsonSerializer.Deserialize<UserContextDto>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return userContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token with auth service");
            throw;
        }
    }
}
