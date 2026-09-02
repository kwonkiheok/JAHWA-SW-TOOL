namespace MeasureHub.ViewModels;

public sealed class SettlingResultRow
{
    public string FileName { get; set; } = "";
    public string GroupLabel { get; set; } = "";
    public double? Target { get; set; }
    public double? SettlingTimeMs { get; set; }
    public double? Overshoot { get; set; }
    public double? Displacement { get; set; }
    public string Judge { get; set; } = "";
    public string JudgeReason { get; set; } = "";
    public string PlotPath { get; set; } = "";
}
