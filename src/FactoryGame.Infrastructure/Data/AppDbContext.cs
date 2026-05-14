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
    public DbSet<BoardEntity> Boards => Set<BoardEntity>();
    public DbSet<BoardRevisionEntity> BoardRevisions => Set<BoardRevisionEntity>();
    public DbSet<PlayerMachineStockEntity> PlayerMachineStocks => Set<PlayerMachineStockEntity>();
    public DbSet<SimulationClockEntity> SimulationClock => Set<SimulationClockEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GuestDeviceKeyHash).IsUnique();
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
            e.HasIndex(x => new { x.PlayerId, x.ElementId }).IsUnique();
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
            e.HasIndex(x => new { x.ElementId, x.Status, x.Side });
            e.HasIndex(x => new { x.PlayerId, x.IdempotencyKey }).IsUnique();
            e.Property(x => x.LimitPrice).HasPrecision(18, 4);
        });

        modelBuilder.Entity<TradeExecutionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ElementId);
            e.Property(x => x.Price).HasPrecision(18, 4);
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
    }
}
