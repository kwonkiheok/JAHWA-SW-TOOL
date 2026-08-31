namespace JahwaInspector.Light.Services;

public sealed record ScanRangeDefinition(double MinInput, double MaxInput);
public sealed record ScanRangeMetrics(double MinInput, double MaxInput, double Hys, double Linearity);

public static class ScanMetrics
{
    public static ScanRangeMetrics ComputeRangeMetrics(
        IReadOnlyList<double> upInput,
        IReadOnlyList<double> upOutput,
        IReadOnlyList<double> downInput,
        IReadOnlyList<double> downOutput,
        ScanRangeDefinition range)
    {
        if (upInput.Count == 0 || upOutput.Count == 0)
            return new ScanRangeMetrics(range.MinInput, range.MaxInput, double.NaN, double.NaN);

        var up = BuildPairs(upInput, upOutput)
            .Where(p => p.x >= range.MinInput && p.x <= range.MaxInput)
            .OrderBy(p => p.x)
            .ToArray();

        if (up.Length < 2)
            return new ScanRangeMetrics(range.MinInput, range.MaxInput, double.NaN, double.NaN);

        var down = BuildPairs(downInput, downOutput)
            .OrderBy(p => p.x)
            .ToArray();

        double hys = double.NaN;
        if (down.Length >= 2)
        {
            var diffs = new List<double>(up.Length);
            foreach (var p in up)
            {
                // HYS는 Up/Down이 실제로 겹치는 입력 구간에서만 계산합니다.
                // 범위 밖을 끝값으로 외삽하면 값이 과대평가될 수 있습니다.
                if (TryInterpolate(down, p.x, out var yd, clampEdges: false))
                    diffs.Add(Math.Abs(p.y - yd));
            }

            if (diffs.Count > 0)
                hys = diffs.Max();
        }

        double yMin = TryInterpolate(BuildPairs(upInput, upOutput).OrderBy(p => p.x).ToArray(), range.MinInput, out var y0)
            ? y0
            : up[0].y;
        double yMax = TryInterpolate(BuildPairs(upInput, upOutput).OrderBy(p => p.x).ToArray(), range.MaxInput, out var y1)
            ? y1
            : up[^1].y;

        double dx = range.MaxInput - range.MinInput;
        if (Math.Abs(dx) < 1e-12)
            return new ScanRangeMetrics(range.MinInput, range.MaxInput, hys, double.NaN);

        double slope = (yMax - yMin) / dx;

        // LI는 기준선에서 가장 많이 벗어난 편차(부호 유지)를 쓴다.
        // 예전에는 Min() 이라 "기준선 아래로 처진" 쪽만 봤고, 위로 볼록하게 휜 파일은
        // 비선형이 커도 LI가 작게 나왔다. 실제 AKM 샘플(#1_result_scan.csv)에서
        // 설비가 기록한 Linearity 1.9 가 위쪽 편차(+1.9458)였는데 기존 코드는 -1.0167 을 냈다.
        // 판정도 Math.Abs(li) 로 절대값 비교를 하므로 계산도 절대값 기준이어야 앞뒤가 맞는다.
        //
        // 범위는 Up 만 본다(Robocube는 Up+Down). AKM에 Up+Down을 적용하면 같은 샘플에서
        // 2.98 이 나와 설비 기록값과 어긋나므로, 선택 규칙만 맞추고 대상 범위는 유지한다.
        double li = up
            .Select(p => p.y - (yMin + slope * (p.x - range.MinInput)))
            .OrderByDescending(Math.Abs)
            .First();

        return new ScanRangeMetrics(range.MinInput, range.MaxInput, hys, li);
    }

    public static (double[] xs, double[] ys) BuildReferenceLine(
        IReadOnlyList<double> upInput,
        IReadOnlyList<double> upOutput,
        ScanRangeDefinition range)
    {
        var up = BuildPairs(upInput, upOutput).OrderBy(p => p.x).ToArray();
        if (up.Length < 2)
            return (Array.Empty<double>(), Array.Empty<double>());

        if (!TryInterpolate(up, range.MinInput, out var yMin))
            yMin = up[0].y;
        if (!TryInterpolate(up, range.MaxInput, out var yMax))
            yMax = up[^1].y;

        return (new[] { range.MinInput, range.MaxInput }, new[] { yMin, yMax });
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
