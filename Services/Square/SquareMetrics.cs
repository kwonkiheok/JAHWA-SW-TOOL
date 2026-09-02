namespace MeasureHub.Services;

// SQUARE TEST 공용 계산: Target이 바뀌는 지점마다 스텝을 자동으로 잘라서(전환 이전 baseline 구간은
// 트리거가 없어 판정 대상이 아니므로 제외), 각 스텝의 트리거 시점을 t=0으로 잡아
// SettlingMetrics.Compute(SETTLING TIME에서 이미 검증된 로직)를 재사용해 Settling Time/Overshoot를 계산한다.
// Tolerance Band는 스텝마다 이동량(|Target - 스텝 시작 시점 값|) × toleranceBandPercent%로 그때그때
// 계산한다(사용자 설정값, 헤더 값 불신) — 목표의 절대 위치가 아니라 "이번 스텝에서 얼마나 움직였는지" 기준.
// combineUpDown=true면 Up/Down 구분 없이 전체 스텝 중 최댓값 하나만 낸다(AKM), false면 이동 방향(Up=목표가
// 커짐, Down=작아짐)으로 나눠 그룹별 최댓값을 낸다(DW).
// 장비(AKM) 헤더의 "return step,1" 설정과 동일하게, 중심(=시퀀스 맨 처음 값)으로 "돌아오는" 스텝만 판정
// 대상으로 삼는다 — 중심에서 바깥(±amplitude)으로 나가는 스텝은 집계에서 제외한다.
public static class SquareMetrics
{
    public static SquareMetricsResult Compute(SquareReadResult read, double toleranceBandPercent, bool combineUpDown)
    {
        double? maxSettle = null, maxOvershoot = null;
        double? maxSettleUp = null, maxOvershootUp = null;
        double? maxSettleDown = null, maxOvershootDown = null;

        foreach (var axis in read.Axes)
        {
            if (axis.Target.Count == 0) continue;
            double homeTarget = axis.Target[0];

            foreach (var step in DetectSteps(axis))
            {
                if (step.Target != homeTarget) continue; // 복귀 스텝만 판정(바깥으로 나가는 스텝은 제외)

                var segTime = axis.TimeMs.Skip(step.Start).Take(step.End - step.Start).ToList();
                var segValue = axis.Value.Skip(step.Start).Take(step.End - step.Start).ToList();
                if (segTime.Count < 2) continue;

                double displacement = Math.Abs(step.Target - axis.Value[step.Start]);
                double band = displacement * toleranceBandPercent / 100.0;
                var result = SettlingMetrics.Compute(segTime, segValue, step.Target, band, tailFraction: 0.2, judgeStartTimeMs: segTime[0]);
                if (result.SettlingTimeMs is not double settleMs || result.Overshoot is not double overshoot)
                    continue;

                if (combineUpDown)
                {
                    if (maxSettle is null || settleMs > maxSettle) maxSettle = settleMs;
                    if (maxOvershoot is null || overshoot > maxOvershoot) maxOvershoot = overshoot;
                    continue;
                }

                bool isUp = step.Target > axis.Value[step.Start];
                if (isUp)
                {
                    if (maxSettleUp is null || settleMs > maxSettleUp) maxSettleUp = settleMs;
                    if (maxOvershootUp is null || overshoot > maxOvershootUp) maxOvershootUp = overshoot;
                }
                else
                {
                    if (maxSettleDown is null || settleMs > maxSettleDown) maxSettleDown = settleMs;
                    if (maxOvershootDown is null || overshoot > maxOvershootDown) maxOvershootDown = overshoot;
                }
            }
        }

        return new SquareMetricsResult(maxSettle, maxOvershoot, maxSettleUp, maxOvershootUp, maxSettleDown, maxOvershootDown);
    }

    private sealed record Step(int Start, int End, double Target);

    private static List<Step> DetectSteps(SquareAxisSeries axis)
    {
        var steps = new List<Step>();
        int n = Math.Min(axis.TimeMs.Count, Math.Min(axis.Target.Count, axis.Value.Count));
        if (n < 2) return steps;

        int stepStart = -1;
        double currentTarget = axis.Target[0];
        for (int i = 1; i < n; i++)
        {
            if (axis.Target[i] != currentTarget)
            {
                if (stepStart >= 0)
                    steps.Add(new Step(stepStart, i, currentTarget));
                stepStart = i;
                currentTarget = axis.Target[i];
            }
        }

        if (stepStart >= 0)
            steps.Add(new Step(stepStart, n, currentTarget));

        return steps;
    }
}
