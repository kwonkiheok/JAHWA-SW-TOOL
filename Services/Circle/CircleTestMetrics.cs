namespace JahwaInspector.Light.Services;

public sealed record CircleMetricsResult(double MaxErrX, double MaxErrY);

public static class CircleTestMetrics
{
    public static CircleMetricsResult Compute(CircleReadResult read)
    {
        return new CircleMetricsResult(ComputeMaxErr(read.DiffX), ComputeMaxErr(read.DiffY));
    }

    private static double ComputeMaxErr(IReadOnlyList<double> diff)
        => diff.Count == 0 ? 0 : diff.Max(v => Math.Abs(v));
}
