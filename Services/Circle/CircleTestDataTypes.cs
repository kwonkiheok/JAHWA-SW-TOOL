namespace MeasureHub.Services;

public interface ICircleTestDataTypeHandler
{
    string Name { get; }
    string InputHeaderDisplay { get; }
    string OutputHeaderDisplay { get; }
    string HeaderRuleDescription { get; }
    string MetricRuleDescription { get; }

    CircleReadResult Read(string file);
    CircleMetricsResult ComputeMetrics(CircleReadResult read);
}

public sealed class AkmCircleTestDataTypeHandler : ICircleTestDataTypeHandler
{
    public string Name => "AKM";
    public string InputHeaderDisplay => "ReadVt_X/Hall_X, ReadVt_Y/Hall_Y";
    public string OutputHeaderDisplay => "Diff = ReadVt - Hall (MaxErr 재계산)";
    public string HeaderRuleDescription => "time[ms], ReadVt_X/Y, Hall_X/Y, Diff_X/Y (X/Y 판정)";
    public string MetricRuleDescription => "전체 구간 중 |Diff| 최대값(MaxErr) — X/Y 각각 계산";

    public CircleReadResult Read(string file) => CircleTestReader.Read(file);

    public CircleMetricsResult ComputeMetrics(CircleReadResult read) => CircleTestMetrics.Compute(read);
}

public sealed class DwCircleTestDataTypeHandler : ICircleTestDataTypeHandler
{
    public string Name => "DW";
    public string InputHeaderDisplay => "X/Y Target(@12bit), X/Y Position(@12bit)";
    public string OutputHeaderDisplay => "Diff = Target - Position (파일 X/Y Diff, 없으면 직접 계산)";
    public string HeaderRuleDescription => "X/Y Target, X/Y Position, X/Y Diff (@12bit) — 시간 컬럼 없음, 행 인덱스 사용 (X/Y 판정)";
    public string MetricRuleDescription => "전체 구간 중 |Diff| 최대값(MaxErr) — X/Y 각각 계산";

    public CircleReadResult Read(string file) => DwCircleTestReader.Read(file);

    public CircleMetricsResult ComputeMetrics(CircleReadResult read) => CircleTestMetrics.Compute(read);
}

public static class CircleTestDataTypeRegistry
{
    private static readonly IReadOnlyList<ICircleTestDataTypeHandler> Handlers =
        new ICircleTestDataTypeHandler[]
        {
            new AkmCircleTestDataTypeHandler(),
            new DwCircleTestDataTypeHandler(),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static ICircleTestDataTypeHandler Get(string? name)
    {
        return Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Handlers[0];
    }
}
