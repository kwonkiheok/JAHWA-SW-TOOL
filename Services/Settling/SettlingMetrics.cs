namespace MeasureHub.Services;

public sealed record SettlingResult(double? Target, double? SettlingTimeMs, double? Overshoot);

public static class SettlingMetrics
{
    /// <summary>
    /// 시계열에서 Settling Time/Overshoot를 계산합니다.
    /// target이 없으면(Robocube) 구간 후반 tailFraction 비율의 평균을 steady-state 목표값으로 가정합니다.
    /// SettlingTime은 tolerance band(±toleranceBand)를 벗어난 마지막 시점(그 이후로는 계속 band 안에 머무름) 기준입니다.
    /// (설비 측정값과 정의를 맞추기 위해 "벗어난 마지막 시점의 다음 샘플"이 아니라 "벗어난 마지막 시점 그 자체"를 사용합니다.)
    /// judgeStartTimeMs 이전 구간(스텝 트리거 전 baseline)은 판정에서 제외하고, SettlingTime/Overshoot는
    /// 그 시점을 t=0 기준으로 계산합니다(Robocube는 스텝 트리거가 항상 151ms에 있음).
    /// Overshoot는 이동 방향 기준으로 target을 넘어 튀어나간 만큼만 잽니다 — 출발점~target 거리(변위)는
    /// 아직 도달 전일 뿐이라 overshoot에 포함하지 않습니다.
    /// </summary>
    public static SettlingResult Compute(
        IReadOnlyList<double> timeMs,
        IReadOnlyList<double> value,
        double? explicitTarget,
        double toleranceBand,
        double tailFraction,
        double judgeStartTimeMs = 0)
    {
        int n = Math.Min(timeMs.Count, value.Count);
        if (n < 2)
            return new SettlingResult(explicitTarget, null, null);

        double target = explicitTarget ?? TailAverage(value, n, tailFraction);
        double band = Math.Abs(toleranceBand);
        double lo = target - band;
        double hi = target + band;

        double t0 = Math.Max(timeMs[0], judgeStartTimeMs);
        int judgeStartIndex = 0;
        while (judgeStartIndex < n - 1 && timeMs[judgeStartIndex] < t0)
            judgeStartIndex++;

        int lastOutside = judgeStartIndex - 1;
        for (int i = judgeStartIndex; i < n; i++)
        {
            if (value[i] < lo || value[i] > hi)
                lastOutside = i;
        }

        // 정착 시각 = "허용범위를 마지막으로 벗어난 다음 샘플" = 처음으로 범위에 들어와 계속 머무는 시점.
        //
        // 근거: DW SETTLING 파일은 헤더에 허용범위(Settling Tolerance Max/Min = target±100)와
        // 정답(Settling Index 154/255, Settling Time 4/5)이 함께 적혀 있다. 밴드가 명시돼 있으므로
        // 정의를 판별할 수 있는데, 실제 데이터에서 밴드에 처음 들어온 index 가 154/255 이고
        // 스텝 시작(150/250) 기준 4ms/5ms 로 설비값과 정확히 일치한다.
        //
        // 주의: 2026-08-28 에 Robocube LOG(SettlingTimeUp 10.000)를 근거로 이 값을 lastOutside 로
        // 바꾼 적이 있으나 잘못된 판단이었다. Robocube 는 허용범위가 파일에 없어 밴드를 모르는데,
        // 밴드를 ±1.40~2.10 으로 잡으면 lastOutside 가, ±2.15~3.65 로 잡으면 lastOutside+1 이
        // 각각 10ms 를 만든다 — 즉 정의를 판별할 근거가 되지 못한다.
        // 밴드가 명시된 DW 를 기준으로 원래 정의(lastOutside + 1)를 유지한다.
        int settleIndex = Math.Max(judgeStartIndex, Math.Min(lastOutside + 1, n - 1));
        double settlingTimeMs = timeMs[settleIndex] - t0;

        // Overshoot = target을 넘어 튀어나간 만큼(진행 방향 기준)만 잰다. 출발점~target 거리(=변위)는
        // "아직 도달 전"일 뿐 overshoot가 아니므로 제외한다. settleIndex가 아니라 트리거 이후 전체 구간을
        // 본다 — Tolerance Band를 실제 이동량보다 크게 잡으면 settleIndex가 트리거 시점으로 붕괴해서
        // overshoot 탐색 구간이 한 점(아직 안 움직인 시작점)만 남는 문제를 피하기 위함이다.
        double direction = Math.Sign(target - value[judgeStartIndex]);
        double overshoot = 0;
        if (direction != 0)
        {
            for (int i = judgeStartIndex; i < n; i++)
            {
                double excess = direction * (value[i] - target);
                if (excess > overshoot) overshoot = excess;
            }
        }

        return new SettlingResult(target, settlingTimeMs, overshoot);
    }

    /// <summary>
    /// Robocube의 5회 반복(Count) 결과를 SettlingTimeMs 기준 최솟값·최댓값 1개씩 제외 후 평균으로 집계합니다.
    /// (반복 수가 4개 미만이면 규칙을 완화해 전체 평균을 사용합니다.)
    /// </summary>
    public static SettlingResult AggregateExcludingMinMax(IReadOnlyList<SettlingResult> perCount)
    {
        var valid = perCount.Where(r => r.SettlingTimeMs.HasValue).ToList();
        if (valid.Count == 0)
            return new SettlingResult(null, null, null);

        var chosen = valid.Count >= 4
            ? valid.OrderBy(r => r.SettlingTimeMs!.Value).Skip(1).SkipLast(1).ToList()
            : valid;

        if (chosen.Count == 0)
            chosen = valid;

        double? avgTarget = chosen.All(r => r.Target.HasValue) ? chosen.Average(r => r.Target!.Value) : null;
        double avgSettling = chosen.Average(r => r.SettlingTimeMs!.Value);
        double? avgOvershoot = chosen.All(r => r.Overshoot.HasValue) ? chosen.Average(r => r.Overshoot!.Value) : null;

        return new SettlingResult(avgTarget, avgSettling, avgOvershoot);
    }

    /// <summary>
    /// 실제 이동 변위: 시작 구간과 도착 구간 각각을 Tail Window(%)만큼 평균 내서 그 차이를 본다.
    /// (도착 구간은 target 추정에 쓰는 것과 같은 뒤쪽 tailFraction 창, 시작 구간은 그 비율을 앞쪽에 동일하게 적용한 창.)
    /// </summary>
    public static double ComputeDisplacement(IReadOnlyList<double> value, double tailFraction)
    {
        int n = value.Count;
        if (n == 0) return 0;

        return TailAverage(value, n, tailFraction) - HeadAverage(value, n, tailFraction);
    }

    private static double HeadAverage(IReadOnlyList<double> value, int n, double tailFraction)
    {
        double frac = Math.Clamp(tailFraction, 0.01, 1.0);
        int headCount = Math.Clamp((int)Math.Round(n * frac), 1, n);

        double sum = 0;
        for (int i = 0; i < headCount; i++)
            sum += value[i];

        return sum / headCount;
    }

    private static double TailAverage(IReadOnlyList<double> value, int n, double tailFraction)
    {
        double frac = Math.Clamp(tailFraction, 0.01, 1.0);
        int tailCount = Math.Max(1, (int)Math.Round(n * frac));
        int startIdx = Math.Max(0, n - tailCount);

        double sum = 0;
        int count = 0;
        for (int i = startIdx; i < n; i++)
        {
            sum += value[i];
            count++;
        }

        return count > 0 ? sum / count : value[n - 1];
    }
}
