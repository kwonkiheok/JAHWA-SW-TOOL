namespace JahwaInspector.Light.Services;

public sealed record SinewaveMetricsResult(double MaxErrX, double MaxErrY);

public static class SinewaveMetrics
{
    public static SinewaveMetricsResult Compute(SinewaveReadResult read)
    {
        return new SinewaveMetricsResult(ComputeMaxErr(read.DiffX), ComputeMaxErr(read.DiffY));
    }

    private static double ComputeMaxErr(IReadOnlyList<double> diff)
        => diff.Count == 0 ? 0 : diff.Max(v => Math.Abs(v));
}
