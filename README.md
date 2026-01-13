# CatalogAPI - Game Purchase Management System

A production-ready REST API built with .NET 10 and Clean Architecture for managing a game catalog and user purchases with event-driven architecture.

## Project Overview

CatalogAPI enables users to:
- Browse a catalog of games with pagination (20 items per page)
- Initiate game purchases (order placement)
- View their personal game library
- Automatic event-driven processing via RabbitMQ

The API implements the **Manual Outbox Pattern** for transactional consistency, ensuring that purchase events are reliably published even in case of failures. The purchase flow is fully event-driven: games are added to the user's library only after payment approval from PaymentsAPI.

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
- **MassTransit 8.5.7** - Message bus and event handling
- **MassTransit.RabbitMQ 8.5.7** - RabbitMQ integration
- **RabbitMQ 4.0** - Event broker
- **Manual Outbox Pattern** - Transactional event publishing with 5-second batch processing (100 items per batch)

### CQRS & Mediator
- **Mediator 2.1.7** (Source-generated) - Command/Query dispatcher
- **FluentValidation 11.9.0** - Input validation
- **Mapster 7.4.0** - Object mapping

### Resilience & Distributed Patterns
- **Polly 8.6.5** - Retry policies (3 attempts, exponential backoff)
- **Circuit Breaker** - Threshold: 5 failures, Timeout: 30s
- **Manual Outbox Pattern** - Ensures reliable event publishing even in case of failures

### Observability
- **Serilog 8.0.3** - Structured logging
- **Correlation ID** - X-Correlation-Id header for request tracing
- **Health Checks** - PostgreSQL and RabbitMQ monitoring

### API & Documentation
- **Asp.Versioning 8.1.1** - API v1.0 (version via query string or header, optional)
- **Controllers-based** REST API
- **Swagger/OpenAPI** - Interactive API documentation

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

### Data Flow - Purchase Game (Event-Driven)

```
1. User Request (with Bearer token)
   POST /api/games/{gameId}/purchase
    ↓
2. [CorrelationIdMiddleware] - Generate/Extract X-Correlation-Id
    ↓
3. [AuthenticationMiddleware] - Validate token via external service
    ↓
4. [GamesController] → [PurchaseGameCommandHandler]
    ├─→ Validate game exists
    ├─→ Check user hasn't already purchased
    ├─→ Create OutboxMessage (with OrderPlacedEvent JSON + CorrelationId)
    └─→ Save to database (game NOT added to library yet)
    ↓
5. [OutboxProcessorService] (Background Service - 5s interval)
    ├─→ Fetch unprocessed OutboxMessages (batch of 100)
    ├─→ Publish OrderPlacedEvent to RabbitMQ exchange "order-placed-event"
    └─→ Mark as ProcessedAt = DateTime.UtcNow
    ↓
6. PaymentsAPI consumes OrderPlacedEvent
    ├─→ Process payment (simulate)
    └─→ Publish PaymentProcessedEvent to RabbitMQ queue "payment-processed-event"
    ↓
7. CatalogAPI consumes PaymentProcessedEvent
    ├─→ If Status == "Approved":
    │   ├─→ Add game to user's library (UserGame record)
    │   └─→ Save changes
    └─→ If Status == "Rejected": Log and skip
    ↓
8. NotificationsAPI consumes PaymentProcessedEvent
    └─→ If Status == "Approved": Send confirmation email (simulated)
```

## API Endpoints

### Base URL
- **Local Development:** `http://localhost:5000`
- **Docker:** `http://localhost:8080`
- **Swagger UI:** `http://localhost:8080` (root path)

**Note:** API versioning is optional. The default version (1.0) is used automatically. You can specify version via:
- Query string: `?api-version=1.0`
- Header: `api-version: 1.0`

### Endpoints Summary

| Method | Endpoint | Description | Auth Required | Role |
|--------|----------|-------------|---------------|------|
| GET | `/api/games` | List games (paginated) | No | - |
| POST | `/api/games` | Create game | Yes | Admin |
| PUT | `/api/games/{gameId}` | Update game | Yes | Admin |
| DELETE | `/api/games/{gameId}` | Delete game | Yes | Admin |
| POST | `/api/games/{gameId}/purchase` | Initiate purchase | Yes | User |
| GET | `/api/user-games` | Get user's library | Yes | User |
| GET | `/api/user-games/{gameId}` | Get specific game from library | Yes | User |
| GET | `/health` | Health check | No | - |

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
GET /api/games?page=1&pageSize=20

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

### Purchase Game (Initiates Order)
```bash
POST /api/games/{gameId}/purchase

Headers:
  Authorization: Bearer {token}
  X-Correlation-Id: {uuid}  # Optional, auto-generated if missing

Response (201 Created):
{
  "gameId": "uuid",
  "message": "Game purchased successfully"
}

Note: This endpoint publishes OrderPlacedEvent. The game will be added to the user's library 
only after PaymentProcessedEvent with status "Approved" is received from PaymentsAPI.

Error Responses:
  404 Not Found - Game does not exist
  409 Conflict - User has already purchased this game
  401 Unauthorized - Invalid or missing bearer token
  500 Internal Server Error - Event publishing failed
```

### Get User's Game Library
```bash
GET /api/user-games

Headers:
  Authorization: Bearer {token}

Response (200 OK):
{
  "items": [
    {
      "userId": "uuid",
      "gameId": "uuid",
      "purchaseDate": "2026-01-08T14:28:54Z",
      "game": {
        "id": "uuid",
        "name": "God of War",
        "description": "...",
        "price": 59.99,
        "genre": "Action",
        "imageUrl": "...",
        "developer": "Santa Monica Studio",
        "releaseDate": "2018-04-20"
      }
    }
  ],
  "totalCount": 5,
  "pageNumber": 1,
  "pageSize": 5
}

Error Responses:
  401 Unauthorized - Invalid or missing bearer token
```

### Get Specific Game from User's Library
```bash
GET /api/user-games/{gameId}

Headers:
  Authorization: Bearer {token}

Response (200 OK):
{
  "userId": "uuid",
  "gameId": "uuid",
  "purchaseDate": "2026-01-08T14:28:54Z",
  "game": { ... }
}

Error Responses:
  401 Unauthorized - Invalid or missing bearer token
  404 Not Found - Game not found in user's library
```

### Create Game (Admin Only)
```bash
POST /api/games

Headers:
  Authorization: Bearer {admin-token}
  Content-Type: application/json

Body:
{
  "name": "New Game",
  "description": "Game description",
  "price": 59.99,
  "genre": "Action",
  "imageUrl": "https://...",
  "developer": "Developer Name",
  "releaseDate": "2024-01-01T00:00:00Z"
}

Response (201 Created):
{
  "gameId": "uuid",
  "message": "Game created successfully"
}

Error Responses:
  401 Unauthorized - Invalid or missing bearer token
  403 Forbidden - Admin role required
  409 Conflict - Game already exists
```

### Update Game (Admin Only)
```bash
PUT /api/games/{gameId}

Headers:
  Authorization: Bearer {admin-token}
  Content-Type: application/json

Body: { ... }

Response (200 OK):
{
  "message": "Game updated successfully"
}

Error Responses:
  401 Unauthorized - Invalid or missing bearer token
  403 Forbidden - Admin role required
  404 Not Found - Game does not exist
```

### Delete Game (Admin Only)
```bash
DELETE /api/games/{gameId}

Headers:
  Authorization: Bearer {admin-token}

Response (204 No Content)

Error Responses:
  401 Unauthorized - Invalid or missing bearer token
  403 Forbidden - Admin role required
  404 Not Found - Game does not exist
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
  event_type VARCHAR(200) NOT NULL,
  payload TEXT NOT NULL,  -- JSON serialized event (OrderPlacedEvent, etc.)
  correlation_id VARCHAR(100) NOT NULL,
  created_at TIMESTAMP NOT NULL,
  processed_at TIMESTAMP  -- NULL while unprocessed
);

CREATE INDEX IX_OutboxMessages_CorrelationId ON outbox_messages(correlation_id);
CREATE INDEX IX_OutboxMessages_ProcessedAt ON outbox_messages(processed_at);
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
2026-01-11 07:00:00.000 [INF] Starting CatalogAPI application
2026-01-11 07:00:05.123 [INF] POST /api/games/{gameId}/purchase User: user@example.com
2026-01-11 07:00:05.234 [INF] Processing purchase for GameId: {GameId}, UserId: {UserId}, CorrelationId: {CorrelationId}
2026-01-11 07:00:05.345 [INF] OrderPlacedEvent published successfully. Waiting for payment processing...
2026-01-11 07:00:10.456 [INF] Outbox Processor Service: Processing 1 outbox messages
2026-01-11 07:00:10.567 [INF] Processed outbox message {MessageId} of type OrderPlacedEvent
2026-01-11 07:00:15.678 [INF] Processing PaymentProcessedEvent. CorrelationId: {CorrelationId}, Status: Approved
2026-01-11 07:00:15.789 [INF] Game successfully added to user library. UserId: {UserId}, GameId: {GameId}
```

## Authentication

The API expects Bearer tokens in the `Authorization` header:

```bash
# Purchase a game
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -X POST http://localhost:8080/api/games/{gameId}/purchase

# Get user's library
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  http://localhost:8080/api/user-games
```

**Auth Service Validation**
- All tokens are validated via external service at `/api/auth/validate`
- Mock service (development) accepts any token
- Returns fixed user: `{userId: "550e8400-e29b-41d4-a716-446655440000", role: "user", ...}`
- Resilience: 3 retries + Circuit Breaker (threshold 5, timeout 30s)

## Event-Driven Architecture

### Events

#### OrderPlacedEvent
Published by CatalogAPI when a user initiates a purchase:
```csharp
{
    "correlationId": "uuid",
    "userId": "uuid",
    "gameId": "uuid",
    "price": 59.99,
    "occurredAt": "2026-01-11T07:00:00Z"
}
```
- **Exchange:** `order-placed-event` (Topic)
- **Consumers:** PaymentsAPI

#### PaymentProcessedEvent
Published by PaymentsAPI after processing payment:
```csharp
{
    "correlationId": "uuid",
    "userId": "uuid",
    "gameId": "uuid",
    "price": 59.99,
    "status": "Approved" | "Rejected",
    "occurredAt": "2026-01-11T07:00:05Z"
}
```
- **Queue:** `payment-processed-event`
- **Consumers:** CatalogAPI, NotificationsAPI

### Outbox Pattern (Manual Implementation)

The CatalogAPI implements a **manual Outbox Pattern** to ensure reliable event publishing:

1. **Event Publishing:**
   - When `OrderPlacedEvent` needs to be published, it's first saved to `OutboxMessages` table
   - The database transaction commits, ensuring the event is persisted
   - The event is NOT immediately published to RabbitMQ

2. **Background Processing:**
   - `OutboxProcessorService` runs every 5 seconds
   - Fetches up to 100 unprocessed messages from `OutboxMessages`
   - Publishes each event to RabbitMQ via MassTransit
   - Marks messages as processed after successful publishing

3. **Benefits:**
   - **Transactional Consistency:** Events are saved in the same transaction as business data
   - **Reliability:** Events are not lost even if RabbitMQ is temporarily unavailable
   - **Idempotency:** CorrelationId ensures events are not processed twice
   - **Retry Logic:** Failed messages remain unprocessed and will be retried

### Purchase Flow Implementation

```csharp
// 1. PurchaseGameCommandHandler - Initiates order
public async Task<Guid> Handle(PurchaseGameCommand command)
{
    // Validate game exists
    var game = await _gameRepository.GetByIdAsync(command.GameId);
    if (game == null) throw new GameNotFoundException();
    
    // Check if user already owns the game
    var existing = await _userGameRepository.GetByUserAndGameAsync(command.UserId, command.GameId);
    if (existing != null) throw new GameAlreadyPurchasedException();
    
    // Create OrderPlacedEvent (game NOT added to library yet)
    var orderPlacedEvent = new OrderPlacedEvent(command.CorrelationId, command.UserId, command.GameId, game.Price);
    
    // Save to Outbox (will be published by OutboxProcessorService)
    await _outbox.PublishAsync(orderPlacedEvent);
    await _outbox.SaveChangesAndFlushMessagesAsync();
    
    return command.GameId; // Order placed, waiting for payment
}

// 2. ProcessPaymentEventHandler - Adds game to library after payment approval
public async Task Handle(PaymentProcessedEvent message)
{
    if (message.Status != "Approved") return; // Skip if payment rejected
    
    // Add game to user's library
    var userGame = new UserGame 
    { 
        UserId = message.UserId, 
        GameId = message.GameId, 
        PurchaseDate = DateTime.UtcNow 
    };
    
    await _userGameRepository.AddAsync(userGame);
    await _unitOfWork.SaveChangesAsync();
}
```

**Key Properties:**
- **Event-Driven:** Game added to library only after payment approval
- **Idempotency:** CorrelationId prevents duplicate processing
- **Reliability:** Outbox Pattern ensures events are never lost
- **Decoupling:** Services communicate asynchronously via events

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
│   │   ├── Events/                  # OrderPlacedEvent, PaymentProcessedEvent
│   │   ├── Interfaces/              # Repository & UnitOfWork contracts
│   │   └── Exceptions/              # Domain-specific exceptions
│   │
│   ├── CatalogAPI.Application/      # CQRS, handlers, validators
│   │   ├── UseCases/
│   │   │   ├── Games/               # GetGamesQuery, CreateGameCommand, UpdateGameCommand, DeleteGameCommand
│   │   │   └── UserGames/           # PurchaseGameCommand, GetUserGamesQuery, GetUserGameQuery, ProcessPaymentEventHandler
│   │   ├── DTOs/                    # GameDto, UserGameDto, PaginatedResultDto, UserContextDto
│   │   └── Mappings/                # Mapster configurations
│   │
│   ├── CatalogAPI.Infrastructure/   # Data access, external services
│   │   ├── Data/                    # DbContext, migrations
│   │   ├── BackgroundServices/      # OutboxProcessorService
│   │   ├── Services/                # HttpAuthService
│   │   └── Repositories/            # GameRepository, UserGameRepository, OutboxMessageRepository, ManualOutbox, UnitOfWork
│   │
│   ├── CatalogAPI.API/              # REST endpoints, middleware
│   │   ├── Controllers/             # GamesController, UserGamesController
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
- **OutboxProcessorService** runs every 5 seconds
  - Fetches unprocessed OutboxMessages in batches of 100
  - Publishes events to RabbitMQ via MassTransit
  - Marks messages as processed after successful publishing
  - Retries failed messages automatically

### Message Consumers (MassTransit)
- **ProcessPaymentEventConsumer** - Consumes `PaymentProcessedEvent` from PaymentsAPI
  - Listens to queue `fcg.catalog.payment-processed` (configured in Program.cs)
  - Adds game to user's library if payment status is "Approved"
  - Implements idempotency checks to prevent duplicate processing
  - Registered as MassTransit consumer in Program.cs

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
→ Verify MassTransit configuration in Program.cs
→ Check for errors in application logs
→ Verify OutboxMessages table has unprocessed messages (processed_at IS NULL)
```

### PaymentProcessedEvent Not Being Consumed
```
→ Verify queue "payment-processed-event" exists in RabbitMQ
→ Check ProcessPaymentEventHandler is registered
→ Verify PaymentProcessedEvent structure matches expected format
→ Check application logs for handler execution
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
