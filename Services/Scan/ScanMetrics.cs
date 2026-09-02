namespace MeasureHub.Services;

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

        // upAll : 거르지 않은 전체 Up 곡선. 기준선 끝값을 범위 경계에서 읽을 때 쓴다.
        // up    : 범위 안으로 거른 것. 편차(LI)를 볼 대상이다.
        // 둘을 섞으면 안 된다 — 걸러진 배열에서 경계값을 읽으면 경계 바로 바깥이
        // 잘려 있어 이웃 점으로 끌려간다(500 자리에 510 의 값이 들어감).
        var upAll = BuildPairs(upInput, upOutput)
            .OrderBy(p => p.x)
            .ToArray();

        var up = upAll
            .Where(p => p.x >= range.MinInput && p.x <= range.MaxInput)
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

        var (yMin, yMax) = ReferenceEnds(upAll, down, range);

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
        // 편차를 보는 대상은 Up 만이다(Robocube는 Up+Down). AKM에 Up+Down을 적용하면
        // 같은 샘플 0-4080 에서 -2.98 이 나와 설비 기록 1.9 와 크게 어긋난다.
        // 기준선 만드는 법과 선택 규칙(절대값 최대)은 Robocube 와 통일했지만,
        // 편차 대상 범위만은 설비 기록값에 맞춰 Up 으로 유지한다.
        double li = up
            .Select(p => p.y - (yMin + slope * (p.x - range.MinInput)))
            .OrderByDescending(Math.Abs)
            .First();

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
        if (up.Length < 2)
            return (Array.Empty<double>(), Array.Empty<double>());

        var down = BuildPairs(downInput, downOutput).OrderBy(p => p.x).ToArray();
        var (yMin, yMax) = ReferenceEnds(up, down, range);

        return (new[] { range.MinInput, range.MaxInput }, new[] { yMin, yMax });
    }

    // 기준선의 양 끝 y 값. Robocube 와 같은 규칙으로, 범위 양 끝에서
    // Up 과 Down 을 각각 읽어 평균을 쓴다.
    //
    // 스캔은 같은 지점에서 출발해 같은 지점으로 돌아오므로 양 끝의
    // 히스테리시스는 대개 0 이고, 그래서 평균을 써도 값이 거의 그대로다.
    //
    // 단, Down 은 범위 밖으로 외삽하지 않는다(clampEdges: false).
    // 리더가 꺾이는 지점을 Up 에만 넣기 때문에(ScanCsvReader) Down 의 최대 x 는
    // 항상 한 점 모자란다. 여기서 끝값을 끌어다 쓰면 실제로 측정하지 않은
    // 자리에 이웃 값을 채우는 셈이 되어 기준선이 통째로 기울어진다.
    // #1_result_scan.csv 의 0-4080 범위에서 4080 자리에 4063 의 값(4063)이 들어가
    // 기준선 끝이 4079 -> 4071 로 내려앉았고 LI 가 1.95 -> 9.51 로 튀었다.
    //
    // 그래서 양 끝 모두 Down 을 읽을 수 있을 때만 평균을 쓰고,
    // 한쪽이라도 범위를 벗어나면 예전처럼 Up 값만 쓴다.
    private static (double yMin, double yMax) ReferenceEnds(
        (double x, double y)[] up,
        (double x, double y)[] down,
        ScanRangeDefinition range)
    {
        double yMin = TryInterpolate(up, range.MinInput, out var u0) ? u0 : up[0].y;
        double yMax = TryInterpolate(up, range.MaxInput, out var u1) ? u1 : up[^1].y;

        if (down.Length >= 2
            && TryInterpolate(down, range.MinInput, out var d0, clampEdges: false)
            && TryInterpolate(down, range.MaxInput, out var d1, clampEdges: false))
        {
            yMin = (yMin + d0) / 2.0;
            yMax = (yMax + d1) / 2.0;
        }

        return (yMin, yMax);
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
