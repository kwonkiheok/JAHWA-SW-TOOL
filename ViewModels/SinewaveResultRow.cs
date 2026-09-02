namespace MeasureHub.ViewModels;

public sealed class SinewaveResultRow
{
    public string FileName { get; set; } = "";
    public int SampleCount { get; set; }
    public double? MaxErrX { get; set; }
    public double? MaxErrY { get; set; }
    public string Judge { get; set; } = "";
    public string JudgeReason { get; set; } = "";
    public string PlotPathX { get; set; } = "";
    public string PlotPathY { get; set; } = "";
}
