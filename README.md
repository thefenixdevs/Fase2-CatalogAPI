# CatalogAPI - Game Purchase Management System

A production-ready REST API built with .NET 10 and Clean Architecture for managing a game catalog and user purchases with event-driven architecture.

## Project Overview

CatalogAPI enables users to:
- Browse a catalog of games with pagination (20 items per page)
- Purchase games and add them to their personal library
- Automatic event publishing when purchases occur via RabbitMQ

The API implements the **Outbox Pattern** for transactional consistency, ensuring that purchase events are reliably published even in case of failures.

## Technology Stack

### Core Framework
- **.NET 10** with ASP.NET Core
- **C# 13** language features
- **Entity Framework Core 10.0** with Npgsql provider

### Database & Persistence
- **PostgreSQL 16** (Docker-based)
- **EF Core Migrations** for schema management
- **Connection String (Docker):** `Host=postgres;Port=5432;Database=catalogdb;Username=admin;Password=admin123`

### Event-Driven Architecture
- **MassTransit 8.3.4** - CQRS message bus
- **RabbitMQ 4.0** - Event broker
- **Outbox Pattern** - Transactional event publishing with 5-second batch processing (100 items per batch)

### CQRS & Mediator
- **Mediator 2.1.7** (Source-generated) - Command/Query dispatcher
- **FluentValidation 11.9.0** - Input validation
- **Mapster 7.4.0** - Object mapping

### Resilience & Distributed Patterns
- **Polly 8.5.0** - Retry policies (3 attempts, exponential backoff)
- **Circuit Breaker** - Threshold: 5 failures, Timeout: 30s
- **Compensating Transactions** - Rollback mechanism for failed MassTransit publishing

### Observability
- **Serilog 8.0.3** - Structured logging
- **Correlation ID** - X-Correlation-Id header for request tracing
- **Health Checks** - PostgreSQL and RabbitMQ monitoring

### API & Documentation
- **Asp.Versioning 8.1.0** - API v1.0
- **Controllers-based** REST API

### Testing
- **xUnit** - Unit testing framework
- **Testcontainers 4.0.0** - Integration testing with Docker
- **Moq 4.20.72** - Mocking framework
- **FluentAssertions 6.12.2** - Assertion library

### Containerization
- **Docker & Docker Compose** - Full containerization
- **Multi-stage builds** - Optimized runtime images
- **Node.js 22-Alpine** - Mock authentication service

## Architecture

### Clean Architecture (6 Layers)

```
┌─────────────────────────────────────────────────────────┐
│                     API Layer                           │
│  Controllers | Middlewares | Exception Handling         │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│                CrossCutting Layer                       │
│  DI | Serilog | Service Configuration                  │
└──────────────────┬──────────────────────────────────────┘
                   │
    ┌──────────────┼──────────────┐
    │              │              │
┌───▼────────┐ ┌──▼──────────┐ ┌─▼──────────────┐
│ Domain     │ │ Application │ │ Infrastructure │
│ Entities   │ │ CQRS|Handlers│ │ EF Core|Repos  │
│ Interfaces │ │ DTOs|Validators
├────────────┤ ├─────────────┤ ├────────────────┤
│ Game       │ │ Commands    │ │ DbContext      │
│ UserGame   │ │ Queries     │ │ Repositories   │
│ OutboxMsg  │ │ Mapster     │ │ UnitOfWork     │
│ Events     │ │ FluentVal   │ │ HttpAuthService│
└────────────┘ └─────────────┘ └────────────────┘
                   │
        ┌──────────┴──────────┐
        │                     │
   ┌────▼──────┐      ┌──────▼────┐
   │ PostgreSQL│      │ RabbitMQ   │
   │ (Database)│      │ (Events)   │
   └───────────┘      └────────────┘
```

### Data Flow - Purchase Game

```
User Request (with Bearer token)
    ↓
[CorrelationIdMiddleware] - Generate/Extract X-Correlation-Id
    ↓
[AuthenticationMiddleware] - Validate token via external service
    ↓
[GamesController.POST /api/v1/games/{id}/purchase]
    ↓
[PurchaseGameCommand] → [Mediator.Send()]
    ↓
[PurchaseGameCommandHandler]
    ├─→ UnitOfWork.BeginTransaction()
    ├─→ Validate game exists
    ├─→ Check user hasn't already purchased
    ├─→ Create UserGame record
    ├─→ Create OutboxMessage (with OrderPlacedEvent JSON + CorrelationId)
    ├─→ DbContext.SaveChanges()
    ├─→ IPublishEndpoint.Publish(OrderPlacedEvent) via MassTransit
    ├─→ On success: UnitOfWork.Commit()
    └─→ On failure: UnitOfWork.RollbackAndThrow() [Compensating Transaction]
    ↓
[OutboxProcessorService] (Background Service - 5s interval)
    ├─→ Fetch unprocessed OutboxMessages (batch of 100)
    ├─→ Publish each via MassTransit
    └─→ Mark as ProcessedAt = DateTime.UtcNow
```

## API Endpoints

### Base URL
- **Local Development:** `http://localhost:5000`
- **Docker:** `http://localhost:8080`

### Health Check
```bash
GET /health
Response: {
  "status": "Healthy",
  "checks": {
    "postgresql": "Healthy",
    "rabbitmq": "Healthy"
  }
}
```

### Get Games (Paginated)
```bash
GET /api/v1/games?pageNumber=1&pageSize=20

Response: {
  "items": [
    {
      "id": "uuid",
      "name": "God of War",
      "description": "...",
      "price": 59.99,
      "genre": "Action",
      "imageUrl": "...",
      "developer": "Santa Monica Studio",
      "releaseDate": "2018-04-20"
    }
  ],
  "totalCount": 10,
  "pageNumber": 1,
  "pageSize": 20
}
```

### Purchase Game
```bash
POST /api/v1/games/{gameId}/purchase

Headers:
  Authorization: Bearer {token}
  X-Correlation-Id: {uuid}  # Optional, auto-generated if missing

Response (201 Created):
{
  "userGameId": "uuid",
  "userId": "uuid",
  "gameId": "uuid",
  "purchaseDate": "2026-01-08T14:28:54Z"
}

Error Responses:
  404 Not Found - Game does not exist
  409 Conflict - User has already purchased this game
  401 Unauthorized - Invalid or missing bearer token
  500 Internal Server Error - Event publishing failed
```

## Database Schema

### Game
```sql
CREATE TABLE games (
  id UUID PRIMARY KEY,
  name VARCHAR(200) NOT NULL,
  description VARCHAR(1000),
  price DECIMAL(18,2),
  genre VARCHAR(100) NOT NULL,
  image_url VARCHAR(500),
  developer VARCHAR(200) NOT NULL,
  release_date TIMESTAMP
);
```

### UserGame
```sql
CREATE TABLE user_games (
  user_id UUID NOT NULL,
  game_id UUID NOT NULL,
  purchase_date TIMESTAMP NOT NULL,
  PRIMARY KEY (user_id, game_id),
  FOREIGN KEY (game_id) REFERENCES games(id),
  UNIQUE (user_id, game_id)
);
```

### OutboxMessage
```sql
CREATE TABLE outbox_messages (
  id UUID PRIMARY KEY,
  event_type VARCHAR(500) NOT NULL,
  payload TEXT NOT NULL,  -- JSON serialized OrderPlacedEvent
  correlation_id UUID NOT NULL,
  created_at TIMESTAMP NOT NULL,
  processed_at TIMESTAMP  -- NULL while unprocessed
);
```

## Seeded Games (10 items)

| Name | Price | Developer |
|------|-------|-----------|
| God of War | $59.99 | Santa Monica Studio |
| Elden Ring | $59.99 | FromSoftware |
| FIFA 25 | $69.99 | EA Sports |
| Minecraft | $26.95 | Mojang Studios |
| Cyberpunk 2077 | $39.99 | CD Projekt Red |
| The Witcher 3 | $29.99 | CD Projekt Red |
| GTA VI | $69.99 | Rockstar Games |
| Stardew Valley | $14.99 | ConcernedApe |
| Hades II | $29.99 | Supergiant Games |
| Baldur's Gate 3 | $59.99 | Larian Studios |

## Configuration

### appsettings.json (Local Development)
```json
{
  "ConnectionStrings": {
    "CatalogDatabase": "Host=localhost;Port=5432;Database=catalogdb;Username=admin;Password=admin123"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": "5672",
    "Username": "guest",
    "Password": "guest"
  },
  "AuthService": {
    "BaseUrl": "http://localhost:3000"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

### appsettings.Development.json (Docker)
```json
{
  "ConnectionStrings": {
    "CatalogDatabase": "Host=postgres;Port=5432;Database=catalogdb;Username=admin;Password=admin123"
  },
  "RabbitMQ": {
    "Host": "rabbitmq",
    "Port": "5672",
    "Username": "guest",
    "Password": "guest"
  },
  "AuthService": {
    "BaseUrl": "http://auth-service:3000"
  },
  "Serilog": {
    "MinimumLevel": "Debug"
  }
}
```

## Docker Setup

### Prerequisites
- Docker Desktop installed
- Port 5432 (PostgreSQL), 5672 (RabbitMQ), 3000 (Auth Service), 8080 (API) available

### Running with Docker Compose

```bash
# Build and start all services
docker-compose up --build

# Services will be available at:
#   - API: http://localhost:8080
#   - PostgreSQL: localhost:5432
#   - RabbitMQ: localhost:5672
#   - RabbitMQ Management UI: http://localhost:15672 (guest/guest)
#   - Adminer (DB Browser): http://localhost:8081
#   - Auth Service: http://localhost:3000
```

### Docker Compose Services

**postgres**
- PostgreSQL 16 image with catalogdb database
- Volumes: postgres_data (persistent storage)
- Health check: `pg_isready`

**rabbitmq**
- RabbitMQ 4.0-management with admin UI
- Volumes: rabbitmq_data (persistent storage)
- Health check: `rabbitmq-diagnostics -q ping`

**auth-service**
- Node.js 22-Alpine Express.js mock
- Returns fixed user: `{userId: "550e8400-e29b-41d4-a716-446655440000", role: "user", ...}`
- Accepts any Bearer token

**catalogapi**
- Multi-stage build (.NET SDK → Runtime)
- ASPNETCORE_URLS=http://+:8080
- Depends on: postgres, rabbitmq, auth-service

**adminer**
- Database browser UI at http://localhost:8081
- Login with postgres credentials

### Stopping Services
```bash
docker-compose down

# Preserve volumes
docker-compose down -v  # Remove volumes
```

## Local Development Setup

### Prerequisites
- .NET 10 SDK
- PostgreSQL 16 (or use Docker: `docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=admin123 postgres:16`)
- RabbitMQ (or use Docker: `docker run -d -p 5672:5672 -p 15672:15672 rabbitmq:4.0-management`)
- Node.js 22+ (for mock auth service)

### Setup Steps

1. **Clone & Restore**
```bash
cd f:\FIAP\FaseII\Fase2-CatalogAPI
dotnet restore
```

2. **Create Database**
```bash
# Using Docker PostgreSQL
docker run -d \
  --name catalog-postgres \
  -e POSTGRES_DB=catalogdb \
  -e POSTGRES_USER=admin \
  -e POSTGRES_PASSWORD=admin123 \
  -p 5432:5432 \
  postgres:16

# Apply migrations
dotnet ef database update -p src/CatalogAPI.Infrastructure -s src/CatalogAPI.API
```

3. **Start RabbitMQ**
```bash
docker run -d \
  --name catalog-rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:4.0-management
```

4. **Start Mock Auth Service**
```bash
cd src/CatalogAPI.API/auth-service
npm install
npm start

# Running on http://localhost:3000
```

5. **Run API**
```bash
dotnet run --project src/CatalogAPI.API

# Available at http://localhost:5000
```

## Logging

All requests are logged with correlation IDs to `/logs/catalog-{date}.txt`:

```
2026-01-08 14:28:54.760 -03:00 [INF] [f47ac10b-58cc-4372-a567-0e02b2c3d479] Starting CatalogAPI application
2026-01-08 14:28:55.123 -03:00 [INF] [a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6] POST /api/v1/games/a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6/purchase User: user@example.com
2026-01-08 14:28:55.234 -03:00 [DBG] [a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6] Beginning transaction
2026-01-08 14:28:55.345 -03:00 [DBG] [a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6] UserGame created: user_id=550e8400-e29b-41d4-a716-446655440000, game_id=a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6
2026-01-08 14:28:55.456 -03:00 [DBG] [a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6] OutboxMessage created with OrderPlacedEvent
2026-01-08 14:28:55.567 -03:00 [INF] [a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6] Transaction committed successfully
2026-01-08 14:28:55.678 -03:00 [INF] [a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6] OrderPlacedEvent published via MassTransit
```

## Authentication

The API expects Bearer tokens in the `Authorization` header:

```bash
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  http://localhost:8080/api/v1/games/gameid/purchase
```

**Auth Service Validation**
- All tokens are validated via external service at `/api/auth/validate`
- Mock service (development) accepts any token
- Returns fixed user: `{userId: "550e8400-e29b-41d4-a716-446655440000", role: "user", ...}`
- Resilience: 3 retries + Circuit Breaker (threshold 5, timeout 30s)

## Transaction Management (Outbox Pattern)

### Purchase Transaction Flow

```csharp
public async Task<Guid> Handle(PurchaseGameCommand request)
{
    await _unitOfWork.BeginTransaction(); // SQL: BEGIN
    
    var game = await _gameRepository.GetByIdAsync(request.GameId);
    if (game == null) throw new GameNotFoundException();
    
    var existing = await _userGameRepository.GetByUserAndGameAsync(request.UserId, request.GameId);
    if (existing != null) throw new GameAlreadyPurchasedException();
    
    var userGame = new UserGame { UserId = request.UserId, GameId = request.GameId, PurchaseDate = DateTime.UtcNow };
    await _userGameRepository.AddAsync(userGame);
    
    var @event = new OrderPlacedEvent(request.CorrelationId, request.UserId, request.GameId, game.Price);
    var outboxMsg = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        EventType = nameof(OrderPlacedEvent),
        Payload = JsonConvert.SerializeObject(@event),
        CorrelationId = request.CorrelationId,
        CreatedAt = DateTime.UtcNow
    };
    await _outboxRepository.AddAsync(outboxMsg);
    
    await _unitOfWork.SaveChangesAsync(); // SQL: All inserts
    
    try
    {
        await _publishEndpoint.Publish(@event);
        await _unitOfWork.Commit(); // SQL: COMMIT
    }
    catch
    {
        // Compensating Transaction
        await _userGameRepository.RemoveAsync(userGame);
        await _outboxRepository.RemoveAsync(outboxMsg);
        await _unitOfWork.RollbackAndThrow(); // SQL: ROLLBACK + throw
    }
}
```

**Key Properties:**
- **Transactional Consistency:** UserGame + OutboxMessage created atomically
- **Event Sourcing:** OrderPlacedEvent stored in OutboxMessage before publishing
- **Idempotency:** CorrelationId used for deduplication
- **Compensating Transactions:** Automatic rollback on MassTransit failure

## Building & Running Tests

### Unit Tests
```bash
dotnet test tests/CatalogAPI.Tests
```

### Integration Tests (with Testcontainers)
```bash
# Requires Docker running
dotnet test tests/CatalogAPI.Tests --filter "Category=Integration"
```

### Build for Production
```bash
dotnet publish -c Release -o ./publish
```

## Project Structure

```
Fase2-CatalogAPI/
├── src/
│   ├── CatalogAPI.Domain/           # Business logic, entities
│   │   ├── Entities/                # Game, UserGame, OutboxMessage
│   │   ├── Events/                  # OrderPlacedEvent
│   │   ├── Interfaces/              # Repository & UnitOfWork contracts
│   │   └── Exceptions/              # Domain-specific exceptions
│   │
│   ├── CatalogAPI.Application/      # CQRS, handlers, validators
│   │   ├── Commands/                # PurchaseGameCommand
│   │   ├── Queries/                 # GetGamesQuery
│   │   ├── Handlers/                # Command/Query handlers
│   │   ├── DTOs/                    # Data transfer objects
│   │   ├── Validators/              # FluentValidation rules
│   │   └── Mappings/                # Mapster configurations
│   │
│   ├── CatalogAPI.Infrastructure/   # Data access, external services
│   │   ├── Data/                    # DbContext, migrations, repositories
│   │   ├── Services/                # HttpAuthService, OutboxProcessorService
│   │   └── Repositories/            # Implementation of domain interfaces
│   │
│   ├── CatalogAPI.API/              # REST endpoints, middleware
│   │   ├── Controllers/             # GamesController
│   │   ├── Middlewares/             # CorrelationId, Auth, Exception handling
│   │   ├── auth-service/            # Mock Node.js auth service
│   │   ├── Program.cs               # ASP.NET Core setup
│   │   └── appsettings.*.json       # Configuration
│   │
│   └── CatalogAPI.CrossCutting/     # Dependency injection, logging
│       ├── DependencyInjection/     # Service extensions
│       └── Logging/                 # Serilog configuration
│
├── tests/
│   └── CatalogAPI.Tests/            # xUnit + Testcontainers
│       ├── Fixtures/                # Testcontainers setup
│       ├── Commands/                # PurchaseGameCommand tests
│       ├── Queries/                 # GetGamesQuery tests
│       ├── API/                     # Integration tests
│       └── Repositories/            # Repository tests
│
├── docker-compose.yml               # Container orchestration
├── Dockerfile                       # Multi-stage build
└── README.md                        # This file
```

## Key Implementation Details

### Mediator (CQRS)
- Source-generated with Mediator 2.1.7
- Handlers automatically registered via reflection
- Commands & Queries dispatched through IMediator

### Repositories
- Generic repository pattern per aggregate
- AsNoTracking optimization for read operations
- Batch operations for OutboxMessage processing

### Unit of Work
- Transaction management abstraction
- BeginTransaction/Commit/RollbackAndThrow pattern
- Coordinate multiple repositories

### Background Services
- OutboxProcessorService runs every 5 seconds
- Publishes unprocessed OutboxMessages in batches of 100
- Marks messages as processed after successful MassTransit publishing

### Middleware Pipeline
```
Request → CorrelationId → ExceptionHandling → Authentication → Controller
Response ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ←
```

## Troubleshooting

### PostgreSQL Connection Failed
```
Cannot connect to postgres:5432
→ Check docker-compose is running: docker-compose ps
→ Verify connection string in appsettings.json
```

### RabbitMQ Connection Failed
```
Cannot connect to rabbitmq:5672
→ Check RabbitMQ health: http://localhost:15672
→ Verify credentials in appsettings.json
```

### OutboxMessages Not Processing
```
Check OutboxProcessorService logs:
→ Ensure RabbitMQ is healthy
→ Verify MassTransit configuration
→ Check for errors in application logs
```

### EF Core Migrations Failed
```
dotnet ef migrations add [Name] -p src/CatalogAPI.Infrastructure -s src/CatalogAPI.API
→ Ensure PostgreSQL is running
→ Check ConnectionString in appsettings.json
→ Verify OnConfiguring method in CatalogDbContext
```

## Performance Characteristics

- **Pagination:** 20 items per page default, configurable
- **Outbox Batch Size:** 100 messages per processing cycle
- **Outbox Interval:** 5 seconds
- **Auth Service Retries:** 3 attempts with exponential backoff (2s, 4s, 8s)
- **Circuit Breaker Threshold:** 5 consecutive failures
- **Circuit Breaker Timeout:** 30 seconds

## Security Considerations

- ✅ Bearer token validation on sensitive endpoints
- ✅ Correlation ID for request tracing and idempotency
- ✅ Structured exception handling (no stack traces in responses)
- ✅ SQL injection protection via EF Core parameterized queries
- ⚠️ CORS not configured (add as needed for production)
- ⚠️ HTTPS not enforced (enable in production)
- ⚠️ Rate limiting not implemented (consider adding)

## License

Proprietary - FIAP Educational Project

## Support

For issues or questions, refer to project documentation or contact the development team.
