using System.Security.Cryptography;
using System.Text;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class GuestAuthService(
    AppDbContext db,
    IOptions<GameEconomyOptions> economyOptions,
    ILogger<GuestAuthService> logger)
{
    private readonly GameEconomyOptions _economy = economyOptions.Value;

    public async Task<GuestAuthResult> SignInGuestAsync(string deviceKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            throw new ArgumentException("DeviceKey is required.", nameof(deviceKey));

        var hash = Sha256Hex(deviceKey.Trim());
        var player = await db.Players
            .Include(p => p.Balance)
            .FirstOrDefaultAsync(p => p.GuestDeviceKeyHash == hash, cancellationToken);

        var isNewPlayer = false;
        if (player == null)
        {
            isNewPlayer = true;
            var id = Guid.NewGuid();
            player = new PlayerEntity
            {
                Id = id,
                GuestDeviceKeyHash = hash,
                CreatedAt = DateTimeOffset.UtcNow,
                Balance = new PlayerBalanceEntity { PlayerId = id, Cash = _economy.StartingCash },
                Pool = new InventoryPoolEntity
                {
                    PlayerId = id,
                    MaxVolume = _economy.PoolMaxVolume,
                    UsedVolume = 0
                }
            };
            db.Players.Add(player);
            db.EconomyTransactions.Add(new EconomyTransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = id,
                Type = "InitialCash",
                CashDelta = _economy.StartingCash,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await EnsureInventoryPoolAsync(player, isNewPlayer, cancellationToken);

        if (isNewPlayer || db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);

        var token = CreateSessionToken();
        db.PlayerSessions.Add(new PlayerSessionEntity
        {
            Token = token,
            PlayerId = player.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        });

        await db.SaveChangesAsync(cancellationToken);

        // Starter pool is granted on first authenticated call (wallet / market) so login stays fast.
        return new GuestAuthResult(player.Id, token);
    }

    private async Task EnsureInventoryPoolAsync(PlayerEntity player, bool isNewPlayer, CancellationToken ct)
    {
        if (isNewPlayer && player.Pool != null)
            return;

        var pool = await db.InventoryPools.FirstOrDefaultAsync(p => p.PlayerId == player.Id, ct);
        if (pool != null)
            return;

        logger.LogWarning("Player {PlayerId} missing inventory pool; creating one.", player.Id);
        db.InventoryPools.Add(new InventoryPoolEntity
        {
            PlayerId = player.Id,
            MaxVolume = _economy.PoolMaxVolume,
            UsedVolume = 0
        });
    }

    private static string CreateSessionToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Sha256Hex(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public readonly record struct GuestAuthResult(Guid PlayerId, string SessionToken);
