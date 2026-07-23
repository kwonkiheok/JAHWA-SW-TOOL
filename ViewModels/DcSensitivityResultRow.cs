namespace JahwaInspector.Light.ViewModels;

public sealed class DcSensitivityResultRow
{
    public string FileName { get; init; } = "";
    public int HeaderRow { get; init; }
    public string InputColumn { get; init; } = "";
    public string OutputColumn { get; init; } = "";
    public int UpCount { get; init; }
    public int DownCount { get; init; }
    public string PlotPath { get; init; } = "";
}
