namespace MeasureHub.ViewModels;

public sealed class TiltResultRow
{
    public string FileName { get; set; } = "";
    public int UpCount { get; set; }
    public int DownCount { get; set; }
    public double? PeakTiltUp { get; set; }
    public double? PeakTiltDown { get; set; }
    public double? PeakTilt { get; set; }
    public string Judge { get; set; } = "";
    public string JudgeReason { get; set; } = "";
    public string PlotPath { get; set; } = "";
}
