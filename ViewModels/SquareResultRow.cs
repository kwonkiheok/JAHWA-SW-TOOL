namespace MeasureHub.ViewModels;

public sealed class SquareResultRow
{
    public string FileName { get; set; } = "";
    public int SampleCount { get; set; }
    public double? MaxSettlingMs { get; set; }
    public double? MaxOvershoot { get; set; }
    public double? MaxSettlingUpMs { get; set; }
    public double? MaxOvershootUp { get; set; }
    public double? MaxSettlingDownMs { get; set; }
    public double? MaxOvershootDown { get; set; }
    public string Judge { get; set; } = "";
    public string JudgeReason { get; set; } = "";
    public string PlotPath { get; set; } = "";
}
