namespace JahwaInspector.Light.Services;

public static class RobocubeScanMetrics
{
    public static ScanRangeMetrics ComputeRangeMetrics(
        IReadOnlyList<double> upInput,
        IReadOnlyList<double> upOutput,
        IReadOnlyList<double> downInput,
        IReadOnlyList<double> downOutput,
        ScanRangeDefinition range)
    {
        var up = BuildPairs(upInput, upOutput).OrderBy(p => p.x).ToArray();
        var down = BuildPairs(downInput, downOutput).OrderBy(p => p.x).ToArray();
        if (up.Length < 2 || down.Length < 2)
            return new ScanRangeMetrics(range.MinInput, range.MaxInput, double.NaN, double.NaN);

        if (!TryBuildReference(up, down, range, out var yStart, out var yEnd))
            return new ScanRangeMetrics(range.MinInput, range.MaxInput, double.NaN, double.NaN);

        double hys = double.NaN;
        var hysDiffs = new List<double>();
        foreach (var p in up.Where(p => p.x >= range.MinInput && p.x <= range.MaxInput))
        {
            if (TryInterpolate(down, p.x, out var yd, clampEdges: false))
                hysDiffs.Add(Math.Abs(p.y - yd));
        }

        if (hysDiffs.Count > 0)
            hys = hysDiffs.Max();

        var deviations = new List<double>();
        foreach (var p in up.Concat(down).Where(p => p.x >= range.MinInput && p.x <= range.MaxInput))
        {
            double reference = InterpolateReferenceY(range, yStart, yEnd, p.x);
            deviations.Add(p.y - reference);
        }

        double li = deviations.Count == 0
            ? double.NaN
            : deviations.OrderByDescending(Math.Abs).First();

        return new ScanRangeMetrics(range.MinInput, range.MaxInput, hys, li);
    }

    public static (double[] xs, double[] ys) BuildReferenceLine(
        IReadOnlyList<double> upInput,
        IReadOnlyList<double> upOutput,
        IReadOnlyList<double> downInput,
        IReadOnlyList<double> downOutput,
        ScanRangeDefinition range)
    {
        var up = BuildPairs(upInput, upOutput).OrderBy(p => p.x).ToArray();
        var down = BuildPairs(downInput, downOutput).OrderBy(p => p.x).ToArray();
        if (!TryBuildReference(up, down, range, out var yStart, out var yEnd))
            return (Array.Empty<double>(), Array.Empty<double>());

        return (new[] { range.MinInput, range.MaxInput }, new[] { yStart, yEnd });
    }

    private static bool TryBuildReference(
        (double x, double y)[] up,
        (double x, double y)[] down,
        ScanRangeDefinition range,
        out double yStart,
        out double yEnd)
    {
        yStart = 0;
        yEnd = 0;

        if (!TryInterpolate(up, range.MinInput, out var upStart))
            return false;
        if (!TryInterpolate(up, range.MaxInput, out var upEnd))
            return false;
        if (!TryInterpolate(down, range.MinInput, out var downStart))
            return false;
        if (!TryInterpolate(down, range.MaxInput, out var downEnd))
            return false;

        yStart = (upStart + downStart) / 2.0;
        yEnd = (upEnd + downEnd) / 2.0;
        return true;
    }

    private static double InterpolateReferenceY(ScanRangeDefinition range, double yStart, double yEnd, double x)
    {
        double dx = range.MaxInput - range.MinInput;
        if (Math.Abs(dx) < 1e-12)
            return yStart;

        double t = (x - range.MinInput) / dx;
        return yStart + (yEnd - yStart) * t;
    }

    private static (double x, double y)[] BuildPairs(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        int n = Math.Min(xs.Count, ys.Count);
        var arr = new (double x, double y)[n];
        for (int i = 0; i < n; i++)
            arr[i] = (xs[i], ys[i]);
        return arr;
    }

    private static bool TryInterpolate((double x, double y)[] sortedByX, double targetX, out double y, bool clampEdges = true)
    {
        y = 0;
        if (sortedByX.Length == 0)
            return false;
        if (sortedByX.Length == 1)
        {
            y = sortedByX[0].y;
            return true;
        }

        if (targetX <= sortedByX[0].x)
        {
            if (!clampEdges && targetX < sortedByX[0].x)
                return false;
            y = sortedByX[0].y;
            return true;
        }

        int last = sortedByX.Length - 1;
        if (targetX >= sortedByX[last].x)
        {
            if (!clampEdges && targetX > sortedByX[last].x)
                return false;
            y = sortedByX[last].y;
            return true;
        }

        for (int i = 1; i < sortedByX.Length; i++)
        {
            var p0 = sortedByX[i - 1];
            var p1 = sortedByX[i];
            if (targetX > p1.x)
                continue;

            double dx = p1.x - p0.x;
            if (Math.Abs(dx) < 1e-12)
            {
                y = p0.y;
                return true;
            }

            double t = (targetX - p0.x) / dx;
            y = p0.y + (p1.y - p0.y) * t;
            return true;
        }

        return false;
    }
}
