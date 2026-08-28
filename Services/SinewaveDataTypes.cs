namespace JahwaInspector.Light.Services;

public interface ISinewaveDataTypeHandler
{
    string Name { get; }
    string InputHeaderDisplay { get; }
    string OutputHeaderDisplay { get; }
    string HeaderRuleDescription { get; }
    string MetricRuleDescription { get; }

    SinewaveReadResult Read(string file);
    SinewaveMetricsResult ComputeMetrics(SinewaveReadResult read);
}

public sealed class AkmSinewaveDataTypeHandler : ISinewaveDataTypeHandler
{
    public string Name => "AKM";
    public string InputHeaderDisplay => "Posi_X/Hall_X, Posi_Y/Hall_Y";
    public string OutputHeaderDisplay => "Diff = Posi - Hall (MaxErr 재계산)";
    public string HeaderRuleDescription => "time[ms], Posi_X/Y, Hall_X/Y, Diff_X/Y (X/Y 판정)";
    public string MetricRuleDescription => "전체 구간 중 |Diff| 최대값(MaxErr) — X/Y 각각 계산";

    public SinewaveReadResult Read(string file) => AkmSinewaveReader.Read(file);

    public SinewaveMetricsResult ComputeMetrics(SinewaveReadResult read) => SinewaveMetrics.Compute(read);
}

public sealed class DwSinewaveDataTypeHandler : ISinewaveDataTypeHandler
{
    public string Name => "DW";
    public string InputHeaderDisplay => "X/Y Target 12bit, X/Y Position 12bit";
    public string OutputHeaderDisplay => "Diff = Target - Position (직접 계산, MaxErr 재계산)";
    public string HeaderRuleDescription => "X/Y Target 12bit, X/Y Position 12bit (X/Y 판정)";
    public string MetricRuleDescription => "전체 구간 중 |Target - Position| 최대값(MaxErr) — X/Y 각각 계산";

    public SinewaveReadResult Read(string file) => DwSinewaveReader.Read(file);

    public SinewaveMetricsResult ComputeMetrics(SinewaveReadResult read) => SinewaveMetrics.Compute(read);
}

public static class SinewaveDataTypeRegistry
{
    private static readonly IReadOnlyList<ISinewaveDataTypeHandler> Handlers =
        new ISinewaveDataTypeHandler[]
        {
            new AkmSinewaveDataTypeHandler(),
            new DwSinewaveDataTypeHandler(),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static ISinewaveDataTypeHandler Get(string? name)
    {
        return Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Handlers[0];
    }
}
