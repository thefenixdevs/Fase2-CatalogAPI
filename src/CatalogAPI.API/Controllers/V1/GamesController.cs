using Asp.Versioning;
using CatalogAPI.Application.Commands;
using CatalogAPI.Application.DTOs;
using CatalogAPI.Application.Queries;
using CatalogAPI.Domain.Exceptions;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CatalogAPI.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
public class GamesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<GamesController> _logger;

    public GamesController(IMediator mediator, ILogger<GamesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of games
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResultDto<GameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResultDto<GameDto>>> GetGames(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetGamesQuery(page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Purchase a game (requires authentication)
    /// </summary>
    [HttpPost("{gameId}/purchase")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PurchaseGame(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // Get user from context (set by AuthenticationMiddleware)
        var userContext = HttpContext.Items["User"] as UserContextDto;
        if (userContext == null)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        // Get correlation ID from context
        var correlationId = HttpContext.Items["CorrelationId"] as Guid? ?? Guid.NewGuid();

        try
        {
            var command = new PurchaseGameCommand(
                gameId, 
                correlationId, 
                Guid.Parse(userContext.UserId));

            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Game purchase successful. GameId: {GameId}, UserId: {UserId}", 
                gameId, userContext.UserId);

            return CreatedAtAction(
                nameof(GetGames), 
                new { }, 
                new { gameId = result, message = "Game purchased successfully" });
        }
        catch (GameNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (GameAlreadyPurchasedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
