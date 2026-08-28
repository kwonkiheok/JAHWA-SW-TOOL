namespace JahwaInspector.Light.Services;

public interface ISquareTestDataTypeHandler
{
    string Name { get; }
    string InputHeaderDisplay { get; }
    string OutputHeaderDisplay { get; }
    string HeaderRuleDescription { get; }
    string MetricRuleDescription { get; }

    SquareReadResult Read(string file);
    SquareMetricsResult ComputeMetrics(SquareReadResult read, double toleranceBandPercent);
}

public sealed class AkmSquareDataTypeHandler : ISquareTestDataTypeHandler
{
    public string Name => "AKM";
    public string InputHeaderDisplay => "pos(Target), hall(Value)";
    public string OutputHeaderDisplay => "Up/Down 구분 없이 전체 스텝 중 Settling/Overshoot 최댓값 1개";
    public string HeaderRuleDescription => "time[ms], pos, read_vt, hall, read_mv (pos 사각파 스텝 자동 감지)";
    public string MetricRuleDescription => "Tolerance Band = 이동량 × 사용자 지정 %, 스텝별 Settling/Overshoot 계산 후 전체 최댓값";

    public SquareReadResult Read(string file) => AkmSquareReader.Read(file);

    public SquareMetricsResult ComputeMetrics(SquareReadResult read, double toleranceBandPercent)
        => SquareMetrics.Compute(read, toleranceBandPercent, combineUpDown: true);
}

public sealed class DwSquareDataTypeHandler : ISquareTestDataTypeHandler
{
    public string Name => "DW";
    public string InputHeaderDisplay => "Target, X Position, Y Position";
    public string OutputHeaderDisplay => "Up/Down 스텝별 Settling/Overshoot 중 그룹별 최댓값(X/Y 통합)";
    public string HeaderRuleDescription => ",Target,X Position,Y Position (Target 사각파 스텝 자동 감지, X/Y 모두 판정)";
    public string MetricRuleDescription => "Tolerance Band = 이동량 × 사용자 지정 %, 스텝별 Settling/Overshoot 계산 후 Up/Down 그룹 최댓값";

    public SquareReadResult Read(string file) => DwSquareReader.Read(file);

    public SquareMetricsResult ComputeMetrics(SquareReadResult read, double toleranceBandPercent)
        => SquareMetrics.Compute(read, toleranceBandPercent, combineUpDown: false);
}

public static class SquareTestDataTypeRegistry
{
    private static readonly IReadOnlyList<ISquareTestDataTypeHandler> Handlers =
        new ISquareTestDataTypeHandler[]
        {
            new AkmSquareDataTypeHandler(),
            new DwSquareDataTypeHandler(),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static ISquareTestDataTypeHandler Get(string? name)
    {
        return Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Handlers[0];
    }
}
