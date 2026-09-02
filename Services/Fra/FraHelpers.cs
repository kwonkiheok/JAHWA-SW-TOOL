namespace MeasureHub.Services;

public static class FraHelpers
{
    public static double? GainNearestAtHz(IReadOnlyList<BodePoint> pts, double target)
    {
        if (pts == null || pts.Count == 0)
            return null;

        const double tol = 1e-9;

        // 1) 정확히 target Hz인 값이 있으면,
        //    그중 가장 먼저 나온(첫 번째) 값을 사용
        for (int i = 0; i < pts.Count; i++)
        {
            if (Math.Abs(pts[i].FrequencyHz - target) < tol)
                return pts[i].GainDb;
        }

        // 2) 없으면 기존처럼 가장 가까운 점 사용
        int idx = NearestIndexByFreq(pts, target);
        return idx < 0 ? null : pts[idx].GainDb;
    }

    public static int NearestIndexByFreq(IReadOnlyList<BodePoint> pts, double target)
    {
        if (pts.Count == 0) return -1;

        int lo = 0, hi = pts.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            var f = pts[mid].FrequencyHz;
            if (f < target) lo = mid + 1;
            else if (f > target) hi = mid - 1;
            else return mid;
        }

        int idx1 = Math.Clamp(lo, 0, pts.Count - 1);
        int idx0 = Math.Clamp(lo - 1, 0, pts.Count - 1);
        var d0 = Math.Abs(pts[idx0].FrequencyHz - target);
        var d1 = Math.Abs(pts[idx1].FrequencyHz - target);
        return d0 <= d1 ? idx0 : idx1;
    }
}
