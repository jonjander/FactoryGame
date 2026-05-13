using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class AdminService(AppDbContext db, IOptions<AdminOptions> adminOptions)
{
    private readonly AdminOptions _admin = adminOptions.Value;

    public void ValidateBootstrapToken(string? headerToken)
    {
        if (string.IsNullOrWhiteSpace(_admin.BootstrapToken))
            throw new InvalidOperationException("Admin bootstrap is not configured.");
        if (!string.Equals(headerToken, _admin.BootstrapToken, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid admin token.");
    }

    public async Task<(Guid Id, string PlainKey)> CreateApiKeyAsync(Guid playerId, string name, string scopes, CancellationToken ct)
    {
        var exists = await db.Players.AnyAsync(p => p.Id == playerId, ct);
        if (!exists)
            throw new InvalidOperationException("Player not found.");

        var plain = $"fg_{Guid.NewGuid():N}{Guid.NewGuid():N}"[..48];
        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            KeyHash = ApiKeyHash.Sha256Hex(plain),
            PlayerId = playerId,
            Scopes = scopes,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);
        return (entity.Id, plain);
    }
}
