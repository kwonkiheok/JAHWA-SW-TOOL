namespace MeasureHub.ViewModels;

public sealed class FraResultRow
{
    public string FileName { get; init; } = "";
    public double? Gain10Hz { get; init; }
    public double? GM { get; init; }
    public double? GM_Freq { get; init; }
    public double? PM { get; init; }
    public double? PM_Freq { get; init; }
    public double? PM3 { get; init; }
    public double? PM3_Freq { get; init; }
    public string Judge { get; set; } = "";        // FRA "PASS" / "NG"
    public string JudgeReason { get; set; } = "";  // 왜 NG인지(옵션)
}
