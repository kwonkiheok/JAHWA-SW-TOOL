namespace JahwaInspector.Light.Services;

public interface ITiltDataTypeHandler
{
    string Name { get; }
    string InputHeaderDisplay { get; }
    string OutputHeaderDisplay { get; }
    string HeaderRuleDescription { get; }
    string MetricRuleDescription { get; }

    TiltReadResult Read(string file);
    TiltMetricsResult ComputeMetrics(TiltReadResult read);
}

public sealed class RobocubeTiltDataTypeHandler : ITiltDataTypeHandler
{
    public string Name => "robocube";
    public string InputHeaderDisplay => "TiltTx, TiltTy";
    public string OutputHeaderDisplay => "Tilt magnitude = sqrt(Tx²+Ty²) (계산값, 파일의 TiltV 컬럼은 사용 안 함)";
    public string HeaderRuleDescription => "Fixed Up/Down sections (SCAN과 동일 파일)";
    public string MetricRuleDescription => "Up/Down 전체 구간 중 최대 Tilt 크기(Peak)";

    public TiltReadResult Read(string file) => RobocubeTiltReader.Read(file);

    public TiltMetricsResult ComputeMetrics(TiltReadResult read) => RobocubeTiltMetrics.Compute(read);
}

public static class TiltDataTypeRegistry
{
    private static readonly IReadOnlyList<ITiltDataTypeHandler> Handlers =
        new ITiltDataTypeHandler[]
        {
            new RobocubeTiltDataTypeHandler(),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static ITiltDataTypeHandler Get(string? name)
    {
        return Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Handlers[0];
    }
}
