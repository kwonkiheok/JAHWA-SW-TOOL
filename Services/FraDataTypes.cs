namespace JahwaInspector.Light.Services;

/// <summary>
/// FRA(Bode Plot) 데이터 타입(설비사) 핸들러.
///
/// 다른 검사(SCAN·SETTLING·TILT·SINE·CIRCLE·SQUARE·DC)와 동일한 "인터페이스 + 레지스트리" 형태다.
/// 예전에는 FRA만 <c>enum FraDataType</c> 을 키로 정의를 찾아오는 방식이라, 설비사를 추가하려면
/// enum 과 Definitions 를 둘 다 고쳐야 했다. 이제는 레지스트리 한 줄이면 된다.
///
/// FRA 는 설비사마다 "파싱 방법"이 다른 게 아니라 "헤더 이름"만 다르므로,
/// 대부분은 <see cref="HeaderBasedFraDataTypeHandler"/> 에 후보 목록만 넘기면 된다.
/// 파싱 방식 자체가 다른 설비사가 생기면 그때 이 인터페이스를 직접 구현하면 된다.
/// </summary>
public interface IFraDataTypeHandler
{
    string Name { get; }
    IReadOnlyList<string> FrequencyHeaders { get; }
    IReadOnlyList<string> GainHeaders { get; }
    IReadOnlyList<string> PhaseHeaders { get; }

    /// <summary>스펙 패널에 표시할 헤더 규칙 설명.</summary>
    string HeaderDescription { get; }

    List<BodePoint> Read(string file);
}

/// <summary>헤더 이름 후보만 다른 일반적인 경우를 처리하는 기본 구현.</summary>
public sealed class HeaderBasedFraDataTypeHandler : IFraDataTypeHandler
{
    public string Name { get; }
    public IReadOnlyList<string> FrequencyHeaders { get; }
    public IReadOnlyList<string> GainHeaders { get; }
    public IReadOnlyList<string> PhaseHeaders { get; }

    public HeaderBasedFraDataTypeHandler(
        string name,
        IReadOnlyList<string> frequencyHeaders,
        IReadOnlyList<string> gainHeaders,
        IReadOnlyList<string> phaseHeaders)
    {
        Name = name;
        FrequencyHeaders = frequencyHeaders;
        GainHeaders = gainHeaders;
        PhaseHeaders = phaseHeaders;
    }

    public string HeaderDescription =>
        $"Frequency: {string.Join(" / ", FrequencyHeaders)}\n" +
        $"Gain: {string.Join(" / ", GainHeaders)}\n" +
        $"Phase: {string.Join(" / ", PhaseHeaders)}\n" +
        "헤더 없음 또는 일치 실패: 1열 / 2열 / 3열";

    public List<BodePoint> Read(string file) => BodeCsvReader.Read(file, this);

    public override string ToString() => Name;
}

public static class FraDataTypeRegistry
{
    private static readonly string[] AutoFrequencyHeaders = ["Frequency [Hz]", "Frequency", "Freq", "Hz"];
    private static readonly string[] AutoGainHeaders = ["Gain [dB]", "Gain", "Magnitude", "Magnitude [dB]", "Magnitude (dB)"];
    private static readonly string[] AutoPhaseHeaders = ["Phase [deg]", "Phase", "Phase (deg)"];

    private static readonly string[] AkmFrequencyHeaders = ["Frequency [Hz]"];
    private static readonly string[] AkmGainHeaders = ["Gain [dB]"];
    private static readonly string[] AkmPhaseHeaders = ["Phase [deg]"];

    private static readonly string[] DwFrequencyHeaders = ["Frequency"];
    private static readonly string[] DwGainHeaders = ["Magnitude"];
    private static readonly string[] DwPhaseHeaders = ["Phase"];

    // 설비사를 추가하려면 여기 한 줄만 더하면 된다.
    private static readonly IReadOnlyList<IFraDataTypeHandler> Handlers =
        new IFraDataTypeHandler[]
        {
            new HeaderBasedFraDataTypeHandler("AUTO", AutoFrequencyHeaders, AutoGainHeaders, AutoPhaseHeaders),
            new HeaderBasedFraDataTypeHandler("AKM", AkmFrequencyHeaders, AkmGainHeaders, AkmPhaseHeaders),
            new HeaderBasedFraDataTypeHandler("DW", DwFrequencyHeaders, DwGainHeaders, DwPhaseHeaders),
            new HeaderBasedFraDataTypeHandler("ROHM", AutoFrequencyHeaders, AutoGainHeaders, AutoPhaseHeaders),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static IReadOnlyList<IFraDataTypeHandler> All => Handlers;

    public static IFraDataTypeHandler Get(string? name)
        => Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? Handlers[0];
}
