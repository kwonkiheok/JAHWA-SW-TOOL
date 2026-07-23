namespace JahwaInspector.Light.Services;

public enum FraDataType { Auto, AKM, DW, Rohm }

public sealed record FraDataTypeDefinition(
    string Name,
    FraDataType DataType,
    IReadOnlyList<string> FrequencyHeaders,
    IReadOnlyList<string> GainHeaders,
    IReadOnlyList<string> PhaseHeaders)
{
    public string HeaderDescription =>
        $"Frequency: {string.Join(" / ", FrequencyHeaders)}\n" +
        $"Gain: {string.Join(" / ", GainHeaders)}\n" +
        $"Phase: {string.Join(" / ", PhaseHeaders)}\n" +
        "헤더 없음 또는 일치 실패: 1열 / 2열 / 3열";

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

    public static IReadOnlyList<FraDataTypeDefinition> Definitions { get; } =
    [
        new("AUTO", FraDataType.Auto, AutoFrequencyHeaders, AutoGainHeaders, AutoPhaseHeaders),
        new("AKM", FraDataType.AKM, AkmFrequencyHeaders, AkmGainHeaders, AkmPhaseHeaders),
        new("DW", FraDataType.DW, DwFrequencyHeaders, DwGainHeaders, DwPhaseHeaders),
        new("ROHM", FraDataType.Rohm, AutoFrequencyHeaders, AutoGainHeaders, AutoPhaseHeaders),
    ];

    public static FraDataTypeDefinition Get(FraDataType dataType)
        => Definitions.First(x => x.DataType == dataType);
}
