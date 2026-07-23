using JahwaInspector.Light.Services;

namespace JahwaInspector.Light.ViewModels;

public sealed class PlotFileItem : ViewModelBase
{
    public string FileName { get; }
    public string FullPath { get; }
    public IReadOnlyList<BodePoint> Points { get; }
    public bool IsAllOverlayItem { get; }

    public bool IsDcSensitivity { get; }
    public IReadOnlyList<double> DcUpInput { get; }
    public IReadOnlyList<double> DcUpOutput { get; }
    public IReadOnlyList<double> DcDownInput { get; }
    public IReadOnlyList<double> DcDownOutput { get; }

    public bool IsScan { get; }
    public IReadOnlyList<double> ScanUpInput { get; }
    public IReadOnlyList<double> ScanUpOutput { get; }
    public IReadOnlyList<double> ScanDownInput { get; }
    public IReadOnlyList<double> ScanDownOutput { get; }
    public IReadOnlyList<double> ScanRefInput { get; }
    public IReadOnlyList<double> ScanRefOutput { get; }

    public bool IsSettling { get; }
    public IReadOnlyList<(string GroupLabel, IReadOnlyList<SettlingSeries> Series)> SettlingGroups { get; }


    private bool _isOverlaySelected;
    public bool IsOverlaySelected
    {
        get => _isOverlaySelected;
        set { _isOverlaySelected = value; OnPropertyChanged(); }
    }

    private string _judge = "";
    public string Judge
    {
        get => _judge;
        set { _judge = value; OnPropertyChanged(); }
    }

    private string _judgeReason = "";
    public string JudgeReason
    {
        get => _judgeReason;
        set { _judgeReason = value; OnPropertyChanged(); }
    }

    public PlotFileItem(string fileName, string fullPath, IReadOnlyList<BodePoint> points, bool isAllOverlayItem = false)
    {
        FileName = fileName;
        FullPath = fullPath;
        Points = points;
        IsAllOverlayItem = isAllOverlayItem;
        DcUpInput = Array.Empty<double>();
        DcUpOutput = Array.Empty<double>();
        DcDownInput = Array.Empty<double>();
        DcDownOutput = Array.Empty<double>();
        ScanUpInput = Array.Empty<double>();
        ScanUpOutput = Array.Empty<double>();
        ScanDownInput = Array.Empty<double>();
        ScanDownOutput = Array.Empty<double>();
        ScanRefInput = Array.Empty<double>();
        ScanRefOutput = Array.Empty<double>();
        SettlingGroups = Array.Empty<(string, IReadOnlyList<SettlingSeries>)>();
    }

    public PlotFileItem(string fileName, string fullPath,
        IReadOnlyList<double> dcUpInput,
        IReadOnlyList<double> dcUpOutput,
        IReadOnlyList<double> dcDownInput,
        IReadOnlyList<double> dcDownOutput)
    {
        FileName = fileName;
        FullPath = fullPath;
        Points = Array.Empty<BodePoint>();
        IsAllOverlayItem = false;
        IsDcSensitivity = true;
        DcUpInput = dcUpInput;
        DcUpOutput = dcUpOutput;
        DcDownInput = dcDownInput;
        DcDownOutput = dcDownOutput;
        ScanUpInput = Array.Empty<double>();
        ScanUpOutput = Array.Empty<double>();
        ScanDownInput = Array.Empty<double>();
        ScanDownOutput = Array.Empty<double>();
        ScanRefInput = Array.Empty<double>();
        ScanRefOutput = Array.Empty<double>();
        SettlingGroups = Array.Empty<(string, IReadOnlyList<SettlingSeries>)>();
    }

    public PlotFileItem(string fileName, string fullPath,
        IReadOnlyList<double> scanUpInput,
        IReadOnlyList<double> scanUpOutput,
        IReadOnlyList<double> scanDownInput,
        IReadOnlyList<double> scanDownOutput,
        IReadOnlyList<double> scanRefInput,
        IReadOnlyList<double> scanRefOutput)
    {
        FileName = fileName;
        FullPath = fullPath;
        Points = Array.Empty<BodePoint>();
        IsAllOverlayItem = false;
        IsScan = true;
        DcUpInput = Array.Empty<double>();
        DcUpOutput = Array.Empty<double>();
        DcDownInput = Array.Empty<double>();
        DcDownOutput = Array.Empty<double>();
        ScanUpInput = scanUpInput;
        ScanUpOutput = scanUpOutput;
        ScanDownInput = scanDownInput;
        ScanDownOutput = scanDownOutput;
        ScanRefInput = scanRefInput;
        ScanRefOutput = scanRefOutput;
        SettlingGroups = Array.Empty<(string, IReadOnlyList<SettlingSeries>)>();
    }

    public PlotFileItem(string fileName, string fullPath,
        IReadOnlyList<(string GroupLabel, IReadOnlyList<SettlingSeries> Series)> settlingGroups)
    {
        FileName = fileName;
        FullPath = fullPath;
        Points = Array.Empty<BodePoint>();
        IsAllOverlayItem = false;
        DcUpInput = Array.Empty<double>();
        DcUpOutput = Array.Empty<double>();
        DcDownInput = Array.Empty<double>();
        DcDownOutput = Array.Empty<double>();
        ScanUpInput = Array.Empty<double>();
        ScanUpOutput = Array.Empty<double>();
        ScanDownInput = Array.Empty<double>();
        ScanDownOutput = Array.Empty<double>();
        ScanRefInput = Array.Empty<double>();
        ScanRefOutput = Array.Empty<double>();
        IsSettling = true;
        SettlingGroups = settlingGroups;
    }
}
