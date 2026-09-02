namespace MeasureHub.Services;

public interface IScanDataTypeHandler
{
    string Name { get; }
    string InputHeaderDisplay { get; }
    string OutputHeaderDisplay { get; }
    string HeaderRuleDescription { get; }
    string RangeRuleDescription { get; }
    string HysRuleDescription { get; }
    string LiRuleDescription { get; }
    bool UsesAutomaticRange { get; }

    ScanReadResult Read(string file, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates);
    ScanRangeMetrics ComputeMetrics(ScanReadResult read, ScanRangeDefinition range);
    (double[] xs, double[] ys) BuildReferenceLine(ScanReadResult read, ScanRangeDefinition range);
}

public sealed class AkmScanDataTypeHandler : IScanDataTypeHandler
{
    public string Name => "AKM";
    public string InputHeaderDisplay => "position code [dec]";
    public string OutputHeaderDisplay => "output";
    public string HeaderRuleDescription => "Fixed header match";
    public string RangeRuleDescription => "Configured range";
    public string HysRuleDescription => "Max absolute difference between Up and Down output at same input";
    public string LiRuleDescription => "Signed largest absolute Up output error from endpoint reference line";
    public bool UsesAutomaticRange => false;

    public ScanReadResult Read(string file, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates)
        => ScanCsvReader.Read(file, inputCandidates, outputCandidates, 120);

    public ScanRangeMetrics ComputeMetrics(ScanReadResult read, ScanRangeDefinition range)
        => ScanMetrics.ComputeRangeMetrics(read.UpInput, read.UpOutput, read.DownInput, read.DownOutput, range);

    public (double[] xs, double[] ys) BuildReferenceLine(ScanReadResult read, ScanRangeDefinition range)
        => ScanMetrics.BuildReferenceLine(read.UpInput, read.UpOutput, range);
}

public sealed class RobocubeScanDataTypeHandler : IScanDataTypeHandler
{
    public string Name => "robocube";
    public string InputHeaderDisplay => "Target";
    public string OutputHeaderDisplay => "LaserDis";
    public string HeaderRuleDescription => "Fixed Up/Down sections";
    public string RangeRuleDescription => "Auto: actual Target min/max per file";
    public string HysRuleDescription => "Max absolute difference between Up and Down LaserDis at same Target";
    public string LiRuleDescription => "Signed largest absolute LaserDis error from endpoint reference line";
    public bool UsesAutomaticRange => true;

    public ScanReadResult Read(string file, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates)
        => RobocubeScanReader.Read(file);

    public ScanRangeMetrics ComputeMetrics(ScanReadResult read, ScanRangeDefinition range)
        => RobocubeScanMetrics.ComputeRangeMetrics(read.UpInput, read.UpOutput, read.DownInput, read.DownOutput, range);

    public (double[] xs, double[] ys) BuildReferenceLine(ScanReadResult read, ScanRangeDefinition range)
        => RobocubeScanMetrics.BuildReferenceLine(read.UpInput, read.UpOutput, read.DownInput, read.DownOutput, range);
}

public static class ScanDataTypeRegistry
{
    private static readonly IReadOnlyList<IScanDataTypeHandler> Handlers =
        new IScanDataTypeHandler[]
        {
            new AkmScanDataTypeHandler(),
            new RobocubeScanDataTypeHandler(),
        };

    public static IReadOnlyList<string> Names { get; } = Handlers.Select(x => x.Name).ToArray();

    public static IScanDataTypeHandler Get(string? name)
    {
        return Handlers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Handlers[0];
    }
}
