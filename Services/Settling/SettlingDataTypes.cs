namespace JahwaInspector.Light.Services;

/// <summary>
/// SETTLING TIME 데이터 타입(설비사) 핸들러.
///
/// 다른 검사(SCAN·TILT·SINE·CIRCLE·SQUARE·DC)와 동일한 "인터페이스 + 레지스트리" 형태다.
/// 예전에는 SETTLING만 <c>enum SettlingVendor</c> + switch 방식이라, 설비사를 하나 추가하려면
/// enum·Read·FileSearchPattern·JudgeStartTimeMs 등 여러 switch를 찾아 고쳐야 했고
/// 하나라도 빠뜨리면 조용히 잘못 동작했다. 이제는 핸들러 클래스 하나 + 레지스트리 한 줄이면 된다.
/// </summary>
public interface ISettlingDataTypeHandler
{
    /// <summary>화면 콤보박스에 표시되는 이름.</summary>
    string Name { get; }

    /// <summary>Import 시 찾을 파일 패턴. K-star만 xlsx, 나머지는 csv.</summary>
    string FileSearchPattern { get; }

    /// <summary>
    /// 스텝 트리거 시점(ms). 이 시점 이전 구간(이동 전 baseline)은 판정에서 제외한다.
    /// Robocube/K-star는 기록 시작 전 baseline이 섞여 있어 보정이 필요하고,
    /// DW/Anosys는 기록이 트리거 시점(0ms)부터 시작하므로 보정이 필요 없다.
    /// </summary>
    double JudgeStartTimeMs { get; }

    string HeaderRuleDescription { get; }
    string MetricRuleDescription { get; }

    IReadOnlyList<SettlingSeries> Read(string file);
}

public sealed class DwSettlingDataTypeHandler : ISettlingDataTypeHandler
{
    public string Name => "DW";
    public string FileSearchPattern => "*.csv";
    public double JudgeStartTimeMs => 0;
    public string HeaderRuleDescription => "key:value preamble 후 \",Target,X Position,Y Position\" 헤더 (첫 컬럼은 Time)";
    public string MetricRuleDescription => "파일에 적힌 Target을 그대로 사용, 기록 시작이 곧 트리거 시점";

    public IReadOnlyList<SettlingSeries> Read(string file) => SettlingCsvReader.ReadDw(file);
}

public sealed class RobocubeSettlingDataTypeHandler : ISettlingDataTypeHandler
{
    public string Name => "Robocube";
    public string FileSearchPattern => "*.csv";
    public double JudgeStartTimeMs => 151;
    public string HeaderRuleDescription => "2행 헤더 (그룹 라벨행 + Time[ms]/Posi[um] 행), Time[ms] 등장 위치로 그룹 분할";
    public string MetricRuleDescription => "Target은 뒷부분 Tail Window 평균으로 추정, 트리거는 항상 151ms";

    public IReadOnlyList<SettlingSeries> Read(string file) => SettlingCsvReader.ReadRobocube(file);
}

public sealed class KStarSettlingDataTypeHandler : ISettlingDataTypeHandler
{
    public string Name => "K-star";
    public string FileSearchPattern => "*.xlsx";
    public double JudgeStartTimeMs => 8;
    public string HeaderRuleDescription => "xlsx 2개 시트(Group_Data / Settling_Group), Laser* 컬럼을 조건별로 소비";
    public string MetricRuleDescription => "Time 컬럼이 없어 행번호 × 0.1ms 로 합성, 트리거는 약 8ms";

    public IReadOnlyList<SettlingSeries> Read(string file) => SettlingCsvReader.ReadKStar(file);
}

public sealed class AnosysSettlingDataTypeHandler : ISettlingDataTypeHandler
{
    public string Name => "Anosys";
    public string FileSearchPattern => "*.csv";
    public double JudgeStartTimeMs => 0;
    public string HeaderRuleDescription => "헤더 없음 — (Time,Value) 쌍이 반복되는 순수 숫자 파일";
    public string MetricRuleDescription => "Target은 뒷부분 Tail Window 평균으로 추정, 기록 시작이 곧 트리거 시점";

    public IReadOnlyList<SettlingSeries> Read(string file) => SettlingCsvReader.ReadAnosys(file);
}

public static class SettlingDataTypeRegistry
{
    // 설비사를 추가하려면 위에 핸들러 클래스를 만들고 여기 한 줄만 더하면 된다.
    private static readonly IReadOnlyList<ISettlingDataTypeHandler> Handlers =
        new ISettlingDataTypeHandler[]
        {
            new DwSettlingDataTypeHandler(),
            new RobocubeSettlingDataTypeHandler(),
            new KStarSettlingDataTypeHandler(),
            new AnosysSettlingDataTypeHandler(),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static IReadOnlyList<ISettlingDataTypeHandler> All => Handlers;

    public static ISettlingDataTypeHandler Get(string? name)
    {
        return Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Handlers[0];
    }
}
