namespace MeasureHub.Services;

// 파일 하나에 축이 1개(AKM)거나 여러 개(DW: X/Y) 있을 수 있어 리스트로 둔다.
// ToleranceBand는 더 이상 파일 헤더에서 고정값을 가져오지 않고, 사용자가 화면에서 설정한
// %값을 각 스텝의 Target에 곱해서 SquareMetrics에서 그때그때 계산한다.
public sealed record SquareAxisSeries(
    string Label,
    IReadOnlyList<double> TimeMs,
    IReadOnlyList<double> Target,
    IReadOnlyList<double> Value);

public sealed record SquareReadResult(int HeaderRow, IReadOnlyList<SquareAxisSeries> Axes);

// AKM처럼 Up/Down을 합쳐 하나의 최댓값만 낼 때는 MaxSettlingMs/MaxOvershoot에 담기고,
// DW처럼 Up/Down을 분리해서 낼 때는 MaxSettlingUpMs/DownMs 쪽에 담긴다(둘 중 한 쪽만 채워짐).
public sealed record SquareMetricsResult(
    double? MaxSettlingMs, double? MaxOvershoot,
    double? MaxSettlingUpMs, double? MaxOvershootUp,
    double? MaxSettlingDownMs, double? MaxOvershootDown);
