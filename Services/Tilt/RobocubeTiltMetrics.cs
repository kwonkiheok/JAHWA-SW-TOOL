namespace JahwaInspector.Light.Services;

public sealed record TiltMetricsResult(double PeakTiltUp, double PeakTiltDown, double PeakTilt);

public static class RobocubeTiltMetrics
{
    public static TiltMetricsResult Compute(TiltReadResult read)
    {
        double peakUp = PeakMagnitude(read.UpTiltTx, read.UpTiltTy);
        double peakDown = PeakMagnitude(read.DownTiltTx, read.DownTiltTy);
        double peak = Math.Max(peakUp, peakDown);
        return new TiltMetricsResult(peakUp, peakDown, peak);
    }

    private static double PeakMagnitude(IReadOnlyList<double> tx, IReadOnlyList<double> ty)
    {
        int n = Math.Min(tx.Count, ty.Count);
        double peak = 0;
        for (int i = 0; i < n; i++)
        {
            double magnitude = Math.Sqrt(tx[i] * tx[i] + ty[i] * ty[i]);
            if (magnitude > peak)
                peak = magnitude;
        }

        return peak;
    }
}
