namespace JahwaInspector.Light.ViewModels;

public sealed class ScanResultRow
{
    public string FileName { get; set; } = "";
    public int HeaderRow { get; set; }
    public string InputColumn { get; set; } = "";
    public string OutputColumn { get; set; } = "";
    public int UpCount { get; set; }
    public int DownCount { get; set; }
    public string Range1 { get; set; } = "";
    public double? Hys1 { get; set; }
    public double? Li1 { get; set; }
    public string Range2 { get; set; } = "";
    public double? Hys2 { get; set; }
    public double? Li2 { get; set; }
    public string Judge { get; set; } = "";
    public string JudgeReason { get; set; } = "";
    public string PlotPath { get; set; } = "";
}
