using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Data;

internal static class PlayerSchemaPatches
{
    public static async Task BackfillCreatedAtUtcTicksAsync(AppDbContext db, CancellationToken ct = default)
    {
        var stale = await db.Players
            .Where(p => p.CreatedAtUtcTicks == 0 && p.CreatedAt != default)
            .ToListAsync(ct);
        if (stale.Count == 0)
            return;

        foreach (var player in stale)
            player.CreatedAtUtcTicks = player.CreatedAt.UtcTicks;

        await db.SaveChangesAsync(ct);
    }
}
