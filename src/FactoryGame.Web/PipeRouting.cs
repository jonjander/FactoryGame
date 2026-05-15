namespace FactoryGame.Web;

internal static class PipeRouting
{
    private const double DefaultBulge = 40;

    internal readonly record struct PipeDraw(string Path, string CssClass);

    internal static string PairKey(string machineA, string machineB) =>
        string.Compare(machineA, machineB, StringComparison.Ordinal) < 0
            ? $"{machineA}\0{machineB}"
            : $"{machineB}\0{machineA}";

    internal static IReadOnlyList<PipeDraw> BuildPaths(
        IReadOnlyList<((double X, double Y) From, (double X, double Y) To, string PairKey)> segments)
    {
        var laneTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var seg in segments)
            laneTotals[seg.PairKey] = laneTotals.GetValueOrDefault(seg.PairKey) + 1;

        var laneCursor = new Dictionary<string, int>(StringComparer.Ordinal);
        var draws = new List<PipeDraw>(segments.Count);

        foreach (var seg in segments)
        {
            var lane = laneCursor.GetValueOrDefault(seg.PairKey);
            laneCursor[seg.PairKey] = lane + 1;
            var laneCount = laneTotals[seg.PairKey];
            draws.Add(new PipeDraw(
                BuildPath(seg.From, seg.To, lane, laneCount),
                laneCount > 1 ? "fg-pipe fg-pipe-loop" : "fg-pipe"));
        }

        return draws;
    }

    internal static string BuildPath((double X, double Y) p1, (double X, double Y) p2, int lane, int laneCount)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (laneCount <= 1)
            return BuildSinglePath(p1, p2, dist);

        var bulge = Math.Clamp(DefaultBulge + (laneCount - 2) * 8, DefaultBulge, 72);
        var offsetSign = lane switch
        {
            0 => -1.0,
            1 => 1.0,
            _ => (lane - (laneCount - 1) / 2.0) * (2.0 / Math.Max(1, laneCount - 1))
        };

        var len = Math.Max(dist, 1);
        var perpX = -dy / len;
        var perpY = dx / len;
        var offset = bulge * offsetSign;

        var c1x = p1.X + dx * 0.28 + perpX * offset;
        var c1y = p1.Y + dy * 0.28 + perpY * offset;
        var c2x = p1.X + dx * 0.72 + perpX * offset;
        var c2y = p1.Y + dy * 0.72 + perpY * offset;

        return $"M {Fmt(p1.X)} {Fmt(p1.Y)} C {Fmt(c1x)} {Fmt(c1y)}, {Fmt(c2x)} {Fmt(c2y)}, {Fmt(p2.X)} {Fmt(p2.Y)}";
    }

    private static string BuildSinglePath((double X, double Y) p1, (double X, double Y) p2, double dist)
    {
        if (dist < 48)
        {
            var bulge = 28;
            var midX = (p1.X + p2.X) / 2;
            var arcY = (p1.Y + p2.Y) / 2 - bulge;
            return $"M {Fmt(p1.X)} {Fmt(p1.Y)} Q {Fmt(midX)} {Fmt(arcY)}, {Fmt(p2.X)} {Fmt(p2.Y)}";
        }

        var mid = (p1.X + p2.X) / 2;
        return $"M {Fmt(p1.X)} {Fmt(p1.Y)} C {Fmt(mid)} {Fmt(p1.Y)}, {Fmt(mid)} {Fmt(p2.Y)}, {Fmt(p2.X)} {Fmt(p2.Y)}";
    }

    private static string Fmt(double v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
