using CatalogAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogAPI.Infrastructure.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext() { }

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Game> Games => Set<Game>();
    public DbSet<UserGame> UserGames => Set<UserGame>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Design-time configuration
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=catalogdb;Username=admin;Password=admin123");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Game entity
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Genre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Developer).IsRequired().HasMaxLength(200);
        });

        // Configure UserGame entity (Many-to-Many relationship)
        modelBuilder.Entity<UserGame>(entity =>
        {
            entity.HasKey(ug => new { ug.UserId, ug.GameId });

            entity.HasOne(ug => ug.Game)
                  .WithMany(g => g.UserGames)
                  .HasForeignKey(ug => ug.GameId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ug => new { ug.UserId, ug.GameId }).IsUnique();
        });

        // Configure OutboxMessage entity
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Payload).IsRequired().HasColumnType("text");
            entity.Property(e => e.CorrelationId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired(false);

            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.ProcessedAt);
        });

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).IsRequired();
            entity.Property(e => e.GameId).IsRequired();
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Status).IsRequired();

            entity.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.Game)
                .WithMany()
                .HasForeignKey(oi => oi.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.GameId);
        });

        // Seed Games
        SeedGames(modelBuilder);
    }

    private static void SeedGames(ModelBuilder modelBuilder)
    {
        var games = new[]
        {
            new Game
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "God of War Ragnarök",
                Description = "Kratos and Atreus embark on a mythic journey for answers before Ragnarök arrives.",
                Price = 59.99m,
                Genre = "Action",
                ImageUrl = "https://image.api.playstation.com/vulcan/ap/rnd/202207/1210/4xJ8XB3bi888QTLZYdl7Oi0s.png",
                Developer = "Santa Monica Studio",
                ReleaseDate = new DateTime(2022, 11, 9, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Elden Ring",
                Description = "A new fantasy action RPG. Rise, Tarnished, and be guided by grace to brandish the power of the Elden Ring.",
                Price = 59.99m,
                Genre = "RPG",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1245620/header.jpg",
                Developer = "FromSoftware",
                ReleaseDate = new DateTime(2022, 2, 25, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "FIFA 25",
                Description = "Feel closer to the game with EA SPORTS FC 25. Experience the most true-to-football experience ever.",
                Price = 69.99m,
                Genre = "Sports",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/2195250/header.jpg",
                Developer = "EA Sports",
                ReleaseDate = new DateTime(2024, 9, 27, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Minecraft",
                Description = "Explore infinite worlds and build everything from simple homes to grand castles.",
                Price = 26.95m,
                Genre = "Sandbox",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1942280/header.jpg",
                Developer = "Mojang Studios",
                ReleaseDate = new DateTime(2011, 11, 18, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Cyberpunk 2077",
                Description = "An open-world, action-adventure RPG set in the megalopolis of Night City.",
                Price = 39.99m,
                Genre = "RPG",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1091500/header.jpg",
                Developer = "CD Projekt Red",
                ReleaseDate = new DateTime(2020, 12, 10, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "The Witcher 3: Wild Hunt",
                Description = "As Geralt of Rivia, a monster hunter for hire, journey through a rich fantasy world.",
                Price = 29.99m,
                Genre = "RPG",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/292030/header.jpg",
                Developer = "CD Projekt Red",
                ReleaseDate = new DateTime(2015, 5, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Name = "Grand Theft Auto VI",
                Description = "Experience the next generation of open-world gaming with unprecedented graphics and gameplay.",
                Price = 69.99m,
                Genre = "Action",
                ImageUrl = "https://media-rockstargames-com.akamaized.net/mfe6/prod/__common/img/71d4d8a6006e148ceb6da1bcbf06c3c5.jpg",
                Developer = "Rockstar Games",
                ReleaseDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                Name = "Stardew Valley",
                Description = "You've inherited your grandfather's old farm plot. Armed with hand-me-down tools, you set out to begin your new life!",
                Price = 14.99m,
                Genre = "Simulation",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/413150/header.jpg",
                Developer = "ConcernedApe",
                ReleaseDate = new DateTime(2016, 2, 26, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                Name = "Hades II",
                Description = "Battle beyond the Underworld using dark sorcery as you take on the Titan of Time in this sequel.",
                Price = 29.99m,
                Genre = "Roguelike",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1145350/header.jpg",
                Developer = "Supergiant Games",
                ReleaseDate = new DateTime(2024, 5, 6, 0, 0, 0, DateTimeKind.Utc)
            },
            new Game
            {
                Id = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"),
                Name = "Baldur's Gate 3",
                Description = "Gather your party and return to the Forgotten Realms in a tale of fellowship and betrayal.",
                Price = 59.99m,
                Genre = "RPG",
                ImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1086940/header.jpg",
                Developer = "Larian Studios",
                ReleaseDate = new DateTime(2023, 8, 3, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        modelBuilder.Entity<Game>().HasData(games);
    }
}
