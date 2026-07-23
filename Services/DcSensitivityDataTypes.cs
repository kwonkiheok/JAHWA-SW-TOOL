namespace JahwaInspector.Light.Services;

public interface IDcSensitivityDataTypeHandler
{
    string Name { get; }
    string InputHeaderDisplay { get; }
    string OutputHeaderDisplay { get; }
    string HeaderRuleDescription { get; }

    DcSensitivityReadResult Read(string file, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates, int headerScanRows);
}

public sealed class AkmDcSensitivityDataTypeHandler : IDcSensitivityDataTypeHandler
{
    public string Name => "AKM";
    public string InputHeaderDisplay => "position code [dec]";
    public string OutputHeaderDisplay => "output";
    public string HeaderRuleDescription => "후보명으로 헤더 탐색(Fixed header match)";

    public DcSensitivityReadResult Read(string file, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates, int headerScanRows)
        => DcSensitivityCsvReader.Read(file, inputCandidates, outputCandidates, headerScanRows);
}

// DW: "Loop mode/Start Target/I2C Read/Delay/Step Target" 등 key:value preamble 뒤에
// "DAC,LDM1_0,LDM2_0,HallA_0,HallB_0,Position_0,SubPosition_0,IOUT_0" 헤더가 온다.
// Input=DAC, Output=Position_0로 사용자 확정 — preamble 때문에 헤더 탐색 로직(DcSensitivityCsvReader)을
// 그대로 재사용하되 후보명만 고정한다(다른 컬럼은 사용하지 않음).
public sealed class DwDcSensitivityDataTypeHandler : IDcSensitivityDataTypeHandler
{
    private static readonly string[] InputCandidates = { "DAC" };
    private static readonly string[] OutputCandidates = { "Position_0" };

    public string Name => "DW";
    public string InputHeaderDisplay => "DAC";
    public string OutputHeaderDisplay => "Position_0";
    public string HeaderRuleDescription => "Fixed header match (preamble skip)";

    public DcSensitivityReadResult Read(string file, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates, int headerScanRows)
        => DcSensitivityCsvReader.Read(file, InputCandidates, OutputCandidates, headerScanRows);
}

public static class DcSensitivityDataTypeRegistry
{
    private static readonly IReadOnlyList<IDcSensitivityDataTypeHandler> Handlers =
        new IDcSensitivityDataTypeHandler[]
        {
            new AkmDcSensitivityDataTypeHandler(),
            new DwDcSensitivityDataTypeHandler(),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static IDcSensitivityDataTypeHandler Get(string? name)
        => Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? Handlers[0];
}
