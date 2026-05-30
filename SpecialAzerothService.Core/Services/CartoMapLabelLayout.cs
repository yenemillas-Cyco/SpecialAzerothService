namespace SpecialAzerothService.Core.Services;

/// <summary>Décale les étiquettes carte pour limiter les superpositions (pastilles inchangées).</summary>
public static class CartoMapLabelLayout
{
    public const double GapAboveDot = 2;

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

        var sorted = requests
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var placed = new List<Box>();
        var results = new List<LabelPosition>(sorted.Count);

        foreach (var req in sorted)
        {
            var (left, top) = FindBestPosition(req, placed, mapWidth, mapHeight, padding);
            placed.Add(new Box(left, top, req.Width, req.Height));
            results.Add(new LabelPosition { Key = req.Key, Left = left, Top = top });
        }

        return results;
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
        double padding)
    {
        (double Left, double Top)? best = null;
        var bestScore = double.MaxValue;

        foreach (var candidate in EnumerateCandidates(req))
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
        var (x2, y2) = LabelEdgeMidpointToward(anchorX, anchorY, labelLeft, labelTop, labelWidth, labelHeight);
        return (x1, y1, x2, y2);
    }

    /// <summary>Milieu du côté de l'étiquette orienté vers la pastille (le trait ne traverse pas le fond).</summary>
    private static (double X, double Y) LabelEdgeMidpointToward(
        double anchorX,
        double anchorY,
        double left,
        double top,
        double width,
        double height)
    {
        var cx = left + width / 2;
        var cy = top + height / 2;
        var dx = anchorX - cx;
        var dy = anchorY - cy;

        if (Math.Abs(dx) * height > Math.Abs(dy) * width)
        {
            var x = dx > 0 ? left + width : left;
            return (x, cy);
        }

        var y = dy > 0 ? top + height : top;
        return (cx, y);
    }

    private static IEnumerable<(double Left, double Top)> EnumerateCandidates(LabelRequest req)
    {
        var centerLeft = req.AnchorX - req.Width / 2;
        var aboveTop = req.AnchorY - req.DotRadius - req.Height - GapAboveDot;
        var belowTop = req.AnchorY + req.DotRadius + GapAboveDot;
        var stepY = req.Height * 0.42 + 1;
        var stepX = Math.Max(3, req.Width * 0.14);

        yield return (centerLeft, aboveTop);

        for (var tier = 1; tier <= 5; tier++)
        {
            yield return (centerLeft, aboveTop - tier * stepY);
            yield return (centerLeft + tier * stepX, aboveTop);
            yield return (centerLeft - tier * stepX, aboveTop);
            yield return (centerLeft + tier * stepX, aboveTop - tier * stepY * 0.35);
            yield return (centerLeft - tier * stepX, aboveTop - tier * stepY * 0.35);
        }

        yield return (centerLeft, belowTop);
        for (var tier = 1; tier <= 3; tier++)
            yield return (centerLeft, belowTop + tier * stepY);

        for (var ring = 1; ring <= 3; ring++)
        {
            var dist = req.DotRadius + req.Height * 0.4 + ring * (req.Height * 0.22);
            const int steps = 7;
            for (var s = 0; s < steps; s++)
            {
                var angle = -Math.PI / 2 + (s / (double)(steps - 1)) * Math.PI;
                var cx = req.AnchorX + Math.Cos(angle) * dist;
                var cy = req.AnchorY + Math.Sin(angle) * dist;
                yield return (cx - req.Width / 2, cy - req.Height / 2);
            }
        }
    }

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
