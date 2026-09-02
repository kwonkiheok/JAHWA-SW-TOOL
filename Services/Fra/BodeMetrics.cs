using System;
using System.Collections.Generic;
using System.Linq;

namespace MeasureHub.Services;

public sealed record BodeMetricsResult(
    double? GainMarginDb,
    double? PhaseMarginDeg,
    double? GmFreqHz,
    double? PmFreqHz,
    double? PhaseMarginAtMinus3dBDeg,
    double? PmMinus3dBFreqHz
);

public static class BodeMetrics
{
    private sealed record GainCrossing(int SegmentIndex, double FrequencyHz, double PhaseMarginDeg);

    public static BodeMetricsResult Extract(IReadOnlyList<BodePoint> pts, bool useCrossGuard = false)
    {
        if (pts == null || pts.Count < 2)
            return new(null, null, null, null, null, null);

        var p2 = pts
            .Where(p => p.FrequencyHz > 0 && !double.IsNaN(p.GainDb) && !double.IsNaN(p.PhaseDeg))
            .OrderBy(p => p.FrequencyHz)
            .ToList();

        if (p2.Count < 2)
            return new(null, null, null, null, null, null);

        var f = p2.Select(p => p.FrequencyHz).ToArray();
        var g = p2.Select(p => p.GainDb).ToArray();
        var wrapped = p2.Select(p => WrapTo180(p.PhaseDeg)).ToArray();
        var unwrapped = UnwrapDegrees(wrapped);

        var pmCrossings = FindGainCrossings(f, g, unwrapped, targetGainDb: 0.0, startSegmentIndex: 0);
        var pmPick = pmCrossings
            .OrderBy(c => c.PhaseMarginDeg)
            .ThenBy(c => c.FrequencyHz)
            .FirstOrDefault();

        double? pm3 = null;
        double? pm3F = null;
        if (pmPick is not null)
        {
            int pm3StartSegment = pmPick.SegmentIndex + 1;
            var pm3Pick = FindGainCrossings(f, g, unwrapped, targetGainDb: -3.0, startSegmentIndex: pm3StartSegment)
                .OrderBy(c => c.FrequencyHz)
                .FirstOrDefault();
            if (pm3Pick is not null)
            {
                pm3 = pm3Pick.PhaseMarginDeg;
                pm3F = pm3Pick.FrequencyHz;
            }
        }

        var (gm, gmF) = FirstPhaseCrossingGm(f, g, unwrapped, targetPhaseDeg: -180.0);

        return new(
            gm,
            pmPick?.PhaseMarginDeg,
            gmF,
            pmPick?.FrequencyHz,
            pm3,
            pm3F);
    }

    private static List<GainCrossing> FindGainCrossings(
        double[] f,
        double[] g,
        double[] phaseUnwrappedDeg,
        double targetGainDb,
        int startSegmentIndex)
    {
        var crossings = new List<GainCrossing>();
        int start = Math.Max(0, startSegmentIndex);

        for (int i = start; i < f.Length - 1; i++)
        {
            double y1 = g[i] - targetGainDb;
            double y2 = g[i + 1] - targetGainDb;

            if (!IsCrossing(y1, y2))
                continue;

            if (AlmostZero(y1) && AlmostZero(y2))
                continue;

            double t = SolveTForCrossing(y1, y2);
            double fc = InterpLogX(f[i], f[i + 1], t);
            double ph = InterpLinear(phaseUnwrappedDeg[i], phaseUnwrappedDeg[i + 1], t);
            double pm = Normalize360(180.0 + ph);
            crossings.Add(new GainCrossing(i, fc, pm));
        }

        return crossings;
    }

    private static (double? gmDb, double? freqHz) FirstPhaseCrossingGm(
        double[] f, double[] g, double[] phaseUnwrappedDeg, double targetPhaseDeg)
    {
        for (int i = 0; i < f.Length - 1; i++)
        {
            double y1 = phaseUnwrappedDeg[i] - targetPhaseDeg;
            double y2 = phaseUnwrappedDeg[i + 1] - targetPhaseDeg;

            if (!IsCrossing(y1, y2))
                continue;

            if (AlmostZero(y1) && AlmostZero(y2))
                continue;

            double t = SolveTForCrossing(y1, y2);
            double fc = InterpLogX(f[i], f[i + 1], t);
            double gAt = InterpLinear(g[i], g[i + 1], t);
            return (-gAt, fc);
        }

        return (null, null);
    }

    private static double WrapTo180(double deg)
    {
        double x = deg % 360.0;
        if (x >= 180.0) x -= 360.0;
        if (x < -180.0) x += 360.0;
        return x;
    }

    private static double[] UnwrapDegrees(double[] wrappedDeg)
    {
        var u = new double[wrappedDeg.Length];
        if (wrappedDeg.Length == 0) return u;

        u[0] = wrappedDeg[0];
        double offset = 0.0;

        for (int i = 1; i < wrappedDeg.Length; i++)
        {
            double prev = wrappedDeg[i - 1];
            double curr = wrappedDeg[i];
            double delta = curr - prev;

            if (delta > 180.0) offset -= 360.0;
            else if (delta < -180.0) offset += 360.0;

            u[i] = curr + offset;
        }

        return u;
    }

    private static bool IsCrossing(double y1, double y2)
    {
        if (double.IsNaN(y1) || double.IsNaN(y2)) return false;
        return (y1 == 0.0) || (y2 == 0.0) || (y1 < 0.0 && y2 > 0.0) || (y1 > 0.0 && y2 < 0.0);
    }

    private static bool AlmostZero(double v, double eps = 1e-12) => Math.Abs(v) < eps;

    private static double Normalize360(double deg)
    {
        double x = deg % 360.0;
        if (x < 0) x += 360.0;
        return x;
    }

    private static double SolveTForCrossing(double y1, double y2)
    {
        double denom = y2 - y1;
        if (AlmostZero(denom)) return 0.0;
        double t = (-y1) / denom;
        if (t < 0.0) t = 0.0;
        if (t > 1.0) t = 1.0;
        return t;
    }

    private static double InterpLinear(double a, double b, double t) => a + (b - a) * t;

    private static double InterpLogX(double f1, double f2, double t)
    {
        double lf1 = Math.Log10(Math.Max(f1, 1e-12));
        double lf2 = Math.Log10(Math.Max(f2, 1e-12));
        double lf = lf1 + (lf2 - lf1) * t;
        return Math.Pow(10.0, lf);
    }
}
