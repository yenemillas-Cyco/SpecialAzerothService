namespace SpecialAzerothService.Core.Services;

/// <summary>Décale les étiquettes carte pour limiter les superpositions (pastilles inchangées).</summary>
public static class CartoMapLabelLayout
{
    public const double GapAboveDot = 2;

    /// <summary>Deux pastilles à cette distance (px) ou moins → répartition circulaire commune.</summary>
    private const double ClusterRadiusPx = 12;

    public sealed class LabelRequest
    {
        public required string Key { get; init; }
        public double AnchorX { get; init; }
        public double AnchorY { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public double DotRadius { get; init; }
        public int Priority { get; init; }
    }

    public sealed class LabelPosition
    {
        public required string Key { get; init; }
        public double Left { get; init; }
        public double Top { get; init; }
    }

    public static IReadOnlyList<LabelPosition> Resolve(
        IReadOnlyList<LabelRequest> requests,
        double mapWidth,
        double mapHeight,
        double padding = 2)
    {
        if (requests.Count == 0)
            return [];

        if (mapWidth < 1 || mapHeight < 1)
            return requests.Select(r => DefaultPosition(r)).ToList();

        var clusters = BuildClusters(requests);
        var placed = new List<Box>();
        var byKey = new Dictionary<string, LabelPosition>(StringComparer.OrdinalIgnoreCase);

        foreach (var cluster in clusters.OrderByDescending(c => c.MaxPriority))
        {
            if (cluster.Items.Count >= 2)
                PlaceClusterCircular(cluster, placed, mapWidth, mapHeight, padding, byKey);
            else
                PlaceSingle(cluster.Items[0], placed, mapWidth, mapHeight, padding, byKey);
        }

        return requests.Select(r => byKey[r.Key]).ToList();
    }

    private static void PlaceSingle(
        LabelRequest req,
        List<Box> placed,
        double mapW,
        double mapH,
        double padding,
        Dictionary<string, LabelPosition> byKey)
    {
        var (left, top) = FindBestPosition(req, placed, mapW, mapH, padding, circularHint: null);
        Commit(req, left, top, placed, byKey);
    }

    private static void PlaceClusterCircular(
        Cluster cluster,
        List<Box> placed,
        double mapW,
        double mapH,
        double padding,
        Dictionary<string, LabelPosition> byKey)
    {
        var ordered = cluster.Items
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var maxDot = ordered.Max(i => i.DotRadius);
        var maxH = ordered.Max(i => i.Height);
        var maxW = ordered.Max(i => i.Width);
        var baseRadius = maxDot + maxH * 0.32 + GapAboveDot + maxW * 0.08;

        for (var ring = 0; ring < 5; ring++)
        {
            var radius = baseRadius + ring * (maxH * 0.2 + 3);
            var tentative = new List<(LabelRequest Req, double Left, double Top)>();
            var tempBoxes = new List<Box>();
            var ok = true;

            for (var i = 0; i < ordered.Count; i++)
            {
                var req = ordered[i];
                var (left, top) = CircularSlot(cluster.CenterX, cluster.CenterY, radius, i, ordered.Count, req);
                (left, top) = Clamp(left, top, req.Width, req.Height, mapW, mapH);
                var box = new Box(left, top, req.Width, req.Height);

                if (Overlaps(placed, box, padding) || Overlaps(tempBoxes, box, padding))
                {
                    ok = false;
                    break;
                }

                tentative.Add((req, left, top));
                tempBoxes.Add(box);
            }

            if (!ok)
                continue;

            foreach (var (req, left, top) in tentative)
                Commit(req, left, top, placed, byKey);
            return;
        }

        foreach (var req in ordered)
            PlaceSingle(req, placed, mapW, mapH, padding, byKey);
    }

    private static (double Left, double Top) CircularSlot(
        double cx, double cy, double radius, int index, int count, LabelRequest req)
    {
        var angle = -Math.PI / 2 + (2 * Math.PI * index / count);
        var labelCx = cx + Math.Cos(angle) * radius;
        var labelCy = cy + Math.Sin(angle) * radius;
        return (labelCx - req.Width / 2, labelCy - req.Height / 2);
    }

    private static void Commit(
        LabelRequest req,
        double left,
        double top,
        List<Box> placed,
        Dictionary<string, LabelPosition> byKey)
    {
        placed.Add(new Box(left, top, req.Width, req.Height));
        byKey[req.Key] = new LabelPosition { Key = req.Key, Left = left, Top = top };
    }

    private static List<Cluster> BuildClusters(IReadOnlyList<LabelRequest> requests)
    {
        var remaining = requests.ToList();
        var clusters = new List<Cluster>();

        while (remaining.Count > 0)
        {
            var seed = remaining[0];
            remaining.RemoveAt(0);
            var cluster = new Cluster();
            cluster.Items.Add(seed);
            cluster.CenterX = seed.AnchorX;
            cluster.CenterY = seed.AnchorY;

            for (var i = remaining.Count - 1; i >= 0; i--)
            {
                var other = remaining[i];
                if (!BelongsToCluster(other, cluster))
                    continue;

                remaining.RemoveAt(i);
                cluster.Items.Add(other);
                cluster.CenterX = cluster.Items.Average(x => x.AnchorX);
                cluster.CenterY = cluster.Items.Average(y => y.AnchorY);
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    private static bool BelongsToCluster(LabelRequest req, Cluster cluster)
    {
        foreach (var member in cluster.Items)
        {
            var dx = req.AnchorX - member.AnchorX;
            var dy = req.AnchorY - member.AnchorY;
            if (dx * dx + dy * dy <= ClusterRadiusPx * ClusterRadiusPx)
                return true;
        }

        return false;
    }

    private sealed class Cluster
    {
        public List<LabelRequest> Items { get; } = [];
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public int MaxPriority => Items.Count == 0 ? 0 : Items.Max(i => i.Priority);
    }

    private static LabelPosition DefaultPosition(LabelRequest req)
    {
        var (left, top) = Clamp(
            req.AnchorX - req.Width / 2,
            req.AnchorY - req.DotRadius - req.Height - GapAboveDot,
            req.Width,
            req.Height,
            double.MaxValue,
            double.MaxValue);
        return new LabelPosition { Key = req.Key, Left = left, Top = top };
    }

    private static (double Left, double Top) FindBestPosition(
        LabelRequest req,
        List<Box> placed,
        double mapW,
        double mapH,
        double padding,
        (double Cx, double Cy, int Count)? circularHint)
    {
        (double Left, double Top)? best = null;
        var bestScore = double.MaxValue;

        IEnumerable<(double Left, double Top)> candidates = circularHint is { } hint
            ? EnumerateCircularCandidates(hint.Cx, hint.Cy, hint.Count, req)
                .Concat(EnumerateCandidates(req))
            : EnumerateCandidates(req);

        foreach (var candidate in candidates)
        {
            var (left, top) = Clamp(candidate.Left, candidate.Top, req.Width, req.Height, mapW, mapH);
            var box = new Box(left, top, req.Width, req.Height);
            var overlap = OverlapArea(box, placed, padding);
            var score = overlap * 10_000 + DistanceScore(req, left, top);
            if (score < bestScore)
            {
                bestScore = score;
                best = (left, top);
                if (overlap <= 0)
                    break;
            }
        }

        if (best.HasValue)
            return best.Value;

        var fallback = DefaultPosition(req);
        return (fallback.Left, fallback.Top);
    }

    private static IEnumerable<(double Left, double Top)> EnumerateCircularCandidates(
        double cx, double cy, int count, LabelRequest req)
    {
        var maxDot = req.DotRadius;
        var baseRadius = maxDot + req.Height * 0.32 + GapAboveDot;
        for (var ring = 0; ring < 4; ring++)
        {
            var radius = baseRadius + ring * (req.Height * 0.2 + 2);
            for (var i = 0; i < Math.Max(count, 6); i++)
                yield return CircularSlot(cx, cy, radius, i, Math.Max(count, 6), req);
        }
    }

    private static double DistanceScore(LabelRequest req, double left, double top)
    {
        var (defLeft, defTop) = DefaultLeftTop(req);
        var dx = left - defLeft;
        var dy = top - defTop;
        return dx * dx + dy * dy;
    }

    public static (double Left, double Top) DefaultLeftTop(LabelRequest req) =>
        (req.AnchorX - req.Width / 2, req.AnchorY - req.DotRadius - req.Height - GapAboveDot);

    /// <summary>Segment pastille → bord de l'étiquette (pour trait de liaison).</summary>
    public static (double X1, double Y1, double X2, double Y2) GetLeaderSegment(
        double anchorX,
        double anchorY,
        double dotRadius,
        double labelLeft,
        double labelTop,
        double labelWidth,
        double labelHeight)
    {
        var cx = labelLeft + labelWidth / 2;
        var cy = labelTop + labelHeight / 2;
        var dx = cx - anchorX;
        var dy = cy - anchorY;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.5)
        {
            var edgeY = anchorY - dotRadius;
            return (anchorX, edgeY, anchorX, labelTop + labelHeight);
        }

        var ux = dx / len;
        var uy = dy / len;
        var x1 = anchorX + ux * dotRadius;
        var y1 = anchorY + uy * dotRadius;
        var (x2, y2) = RayRectIntersection(anchorX, anchorY, ux, uy, labelLeft, labelTop, labelWidth, labelHeight);
        return (x1, y1, x2, y2);
    }

    private static (double X, double Y) RayRectIntersection(
        double ox, double oy, double ux, double uy,
        double left, double top, double width, double height)
    {
        var right = left + width;
        var bottom = top + height;
        var bestT = double.MaxValue;
        var hitX = ox + ux;
        var hitY = oy + uy;

        void TryVertical(double xEdge)
        {
            if (Math.Abs(ux) < 1e-9) return;
            var t = (xEdge - ox) / ux;
            if (t < 1e-6) return;
            var y = oy + uy * t;
            if (y >= top - 0.5 && y <= bottom + 0.5 && t < bestT)
            {
                bestT = t;
                hitX = xEdge;
                hitY = y;
            }
        }

        void TryHorizontal(double yEdge)
        {
            if (Math.Abs(uy) < 1e-9) return;
            var t = (yEdge - oy) / uy;
            if (t < 1e-6) return;
            var x = ox + ux * t;
            if (x >= left - 0.5 && x <= right + 0.5 && t < bestT)
            {
                bestT = t;
                hitX = x;
                hitY = yEdge;
            }
        }

        TryVertical(left);
        TryVertical(right);
        TryHorizontal(top);
        TryHorizontal(bottom);
        return (hitX, hitY);
    }

    private static IEnumerable<(double Left, double Top)> EnumerateCandidates(LabelRequest req)
    {
        var centerLeft = req.AnchorX - req.Width / 2;
        var aboveTop = req.AnchorY - req.DotRadius - req.Height - GapAboveDot;
        var belowTop = req.AnchorY + req.DotRadius + GapAboveDot;
        var stepY = req.Height * 0.42 + 1;
        var stepX = Math.Max(3, req.Width * 0.14);

        yield return (centerLeft, aboveTop);

        for (var tier = 1; tier <= 4; tier++)
        {
            yield return (centerLeft, aboveTop - tier * stepY);
            yield return (centerLeft + tier * stepX, aboveTop);
            yield return (centerLeft - tier * stepX, aboveTop);
        }

        yield return (centerLeft, belowTop);
        for (var tier = 1; tier <= 2; tier++)
            yield return (centerLeft, belowTop + tier * stepY);
    }

    private static bool Overlaps(List<Box> placed, Box candidate, double padding) =>
        OverlapArea(candidate, placed, padding) > 0;

    private static double OverlapArea(Box candidate, List<Box> placed, double padding)
    {
        var total = 0.0;
        foreach (var other in placed)
        {
            var ox = Math.Max(0,
                Math.Min(candidate.Right, other.Right + padding) - Math.Max(candidate.Left, other.Left - padding));
            var oy = Math.Max(0,
                Math.Min(candidate.Bottom, other.Bottom + padding) - Math.Max(candidate.Top, other.Top - padding));
            total += ox * oy;
        }

        return total;
    }

    private static (double Left, double Top) Clamp(
        double left,
        double top,
        double width,
        double height,
        double mapW,
        double mapH)
    {
        if (mapW < double.MaxValue)
            left = Math.Clamp(left, 0, Math.Max(0, mapW - width));
        if (mapH < double.MaxValue)
            top = Math.Clamp(top, 0, Math.Max(0, mapH - height));
        return (left, top);
    }

    private readonly struct Box(double left, double top, double width, double height)
    {
        public double Left { get; } = left;
        public double Top { get; } = top;
        public double Width { get; } = width;
        public double Height { get; } = height;
        public double Right => Left + Width;
        public double Bottom => Top + Height;
    }
}
