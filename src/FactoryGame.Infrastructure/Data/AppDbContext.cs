using FactoryGame.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<PlayerBalanceEntity> PlayerBalances => Set<PlayerBalanceEntity>();
    public DbSet<InventoryPoolEntity> InventoryPools => Set<InventoryPoolEntity>();
    public DbSet<PoolStackEntity> PoolStacks => Set<PoolStackEntity>();
    public DbSet<PlayerSessionEntity> PlayerSessions => Set<PlayerSessionEntity>();
    public DbSet<EconomyTransactionEntity> EconomyTransactions => Set<EconomyTransactionEntity>();
    public DbSet<MarketOrderEntity> MarketOrders => Set<MarketOrderEntity>();
    public DbSet<TradeExecutionEntity> TradeExecutions => Set<TradeExecutionEntity>();
    public DbSet<MarketPriceCandleEntity> MarketPriceCandles => Set<MarketPriceCandleEntity>();
    public DbSet<BoardEntity> Boards => Set<BoardEntity>();
    public DbSet<BoardRevisionEntity> BoardRevisions => Set<BoardRevisionEntity>();
    public DbSet<PlayerMachineStockEntity> PlayerMachineStocks => Set<PlayerMachineStockEntity>();
    public DbSet<SimulationClockEntity> SimulationClock => Set<SimulationClockEntity>();
    public DbSet<BoardKeyframeEntity> BoardKeyframes => Set<BoardKeyframeEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<SponsorCompanyEntity> SponsorCompanies => Set<SponsorCompanyEntity>();
    public DbSet<SponsorCompanyOrderEntity> SponsorCompanyOrders => Set<SponsorCompanyOrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GuestDeviceKeyHash).IsUnique();
            e.HasIndex(x => x.CreatedAtUtcTicks);
            e.HasOne(x => x.Balance).WithOne(x => x.Player).HasForeignKey<PlayerBalanceEntity>(x => x.PlayerId);
            e.HasOne(x => x.Pool).WithOne(x => x.Player).HasForeignKey<InventoryPoolEntity>(x => x.PlayerId);
        });

        modelBuilder.Entity<PlayerBalanceEntity>(e =>
        {
            e.HasKey(x => x.PlayerId);
            e.Property(x => x.Cash).HasPrecision(18, 4);
        });

        modelBuilder.Entity<InventoryPoolEntity>(e =>
        {
            e.HasKey(x => x.PlayerId);
            e.HasMany(x => x.Stacks).WithOne(x => x.Pool).HasForeignKey(x => x.PlayerId);
        });

        modelBuilder.Entity<PoolStackEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PlayerId, x.ElementId, x.Dna }).IsUnique();
        });

        modelBuilder.Entity<PlayerSessionEntity>(e =>
        {
            e.HasKey(x => x.Token);
            e.HasIndex(x => x.PlayerId);
        });

        modelBuilder.Entity<EconomyTransactionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PlayerId);
            e.Property(x => x.CashDelta).HasPrecision(18, 4);
        });

        modelBuilder.Entity<MarketOrderEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ElementId, x.Dna, x.Status, x.Side });
            e.HasIndex(x => new { x.ElementId, x.Dna, x.IsSynthetic, x.Status });
            e.HasIndex(x => new { x.PlayerId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.SponsorCompanyId);
            e.Property(x => x.LimitPrice).HasPrecision(18, 4);
        });

        modelBuilder.Entity<TradeExecutionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ElementId, x.Dna });
            e.HasIndex(x => new { x.ElementId, x.Dna, x.CreatedAt });
            e.HasIndex(x => x.BuyerSponsorCompanyId);
            e.HasIndex(x => x.SellerSponsorCompanyId);
            e.Property(x => x.Price).HasPrecision(18, 4);
        });

        modelBuilder.Entity<MarketPriceCandleEntity>(e =>
        {
            e.ToTable("MarketPriceCandles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.ElementId, x.BucketStart }).IsUnique();
            e.Property(x => x.Open).HasPrecision(18, 4);
            e.Property(x => x.High).HasPrecision(18, 4);
            e.Property(x => x.Low).HasPrecision(18, 4);
            e.Property(x => x.Close).HasPrecision(18, 4);
        });

        modelBuilder.Entity<BoardEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PlayerId);
            e.HasOne<PlayerEntity>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BoardRevisionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.BoardId, x.Version }).IsUnique();
            e.HasOne(x => x.Board).WithMany().HasForeignKey(x => x.BoardId);
        });

        modelBuilder.Entity<BoardKeyframeEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.BoardId, x.Tick });
            e.HasOne(x => x.Board).WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerMachineStockEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PlayerId);
            e.HasOne<PlayerEntity>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SimulationClockEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<ApiKeyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => x.PlayerId);
            e.HasOne<PlayerEntity>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SponsorCompanyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PlayerId).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.BudgetRemaining).HasPrecision(18, 4);
            e.Property(x => x.TotalBudget).HasPrecision(18, 4);
            e.Property(x => x.VirtualSpend).HasPrecision(18, 4);
            e.HasOne<PlayerEntity>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.StandingOrders).WithOne(x => x.SponsorCompany).HasForeignKey(x => x.SponsorCompanyId);
        });

        modelBuilder.Entity<SponsorCompanyOrderEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SponsorCompanyId, x.IsActive });
            e.Property(x => x.LimitPrice).HasPrecision(18, 4);
        });
    }

    public override int SaveChanges()
    {
        SyncPlayerCreatedAtUtcTicks();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SyncPlayerCreatedAtUtcTicks();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SyncPlayerCreatedAtUtcTicks();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SyncPlayerCreatedAtUtcTicks();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void SyncPlayerCreatedAtUtcTicks()
    {
        foreach (var entry in ChangeTracker.Entries<PlayerEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.CreatedAtUtcTicks = entry.Entity.CreatedAt.UtcTicks;
        }
    }
}
