using MeasureHub.Services;

namespace MeasureHub.ViewModels;

// tuple 시그니처가 Sinewave 생성자(7개 double 튜플)와 겹쳐 오버로드 모호성 오류(CS0111)가 나므로 별도 타입 사용.
public sealed record CircleSample(double Time, double ReadVtX, double HallX, double DiffX, double ReadVtY, double HallY, double DiffY);

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

    public bool IsTilt { get; }
    public IReadOnlyList<double> TiltUpTx { get; }
    public IReadOnlyList<double> TiltUpTy { get; }
    public IReadOnlyList<double> TiltDownTx { get; }
    public IReadOnlyList<double> TiltDownTy { get; }

    public bool IsSinewave { get; }
    public IReadOnlyList<double> SinewaveTimeMs { get; }
    public IReadOnlyList<double> SinewavePosiX { get; }
    public IReadOnlyList<double> SinewaveHallX { get; }
    public IReadOnlyList<double> SinewaveDiffX { get; }
    public IReadOnlyList<double> SinewavePosiY { get; }
    public IReadOnlyList<double> SinewaveHallY { get; }
    public IReadOnlyList<double> SinewaveDiffY { get; }

    public bool IsCircleTest { get; }
    public IReadOnlyList<double> CircleTimeMs { get; }
    public IReadOnlyList<double> CircleReadVtX { get; }
    public IReadOnlyList<double> CircleHallX { get; }
    public IReadOnlyList<double> CircleDiffX { get; }
    public IReadOnlyList<double> CircleReadVtY { get; }
    public IReadOnlyList<double> CircleHallY { get; }
    public IReadOnlyList<double> CircleDiffY { get; }

    public bool IsSquareTest { get; }
    public IReadOnlyList<SquareAxisSeries> SquareAxes { get; }

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
        TiltUpTx = Array.Empty<double>();
        TiltUpTy = Array.Empty<double>();
        TiltDownTx = Array.Empty<double>();
        TiltDownTy = Array.Empty<double>();
        SinewaveTimeMs = Array.Empty<double>();
        SinewavePosiX = Array.Empty<double>();
        SinewaveHallX = Array.Empty<double>();
        SinewaveDiffX = Array.Empty<double>();
        SinewavePosiY = Array.Empty<double>();
        SinewaveHallY = Array.Empty<double>();
        SinewaveDiffY = Array.Empty<double>();
        CircleTimeMs = Array.Empty<double>();
        CircleReadVtX = Array.Empty<double>();
        CircleHallX = Array.Empty<double>();
        CircleDiffX = Array.Empty<double>();
        CircleReadVtY = Array.Empty<double>();
        CircleHallY = Array.Empty<double>();
        CircleDiffY = Array.Empty<double>();
        SquareAxes = Array.Empty<SquareAxisSeries>();
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
        TiltUpTx = Array.Empty<double>();
        TiltUpTy = Array.Empty<double>();
        TiltDownTx = Array.Empty<double>();
        TiltDownTy = Array.Empty<double>();
        SinewaveTimeMs = Array.Empty<double>();
        SinewavePosiX = Array.Empty<double>();
        SinewaveHallX = Array.Empty<double>();
        SinewaveDiffX = Array.Empty<double>();
        SinewavePosiY = Array.Empty<double>();
        SinewaveHallY = Array.Empty<double>();
        SinewaveDiffY = Array.Empty<double>();
        CircleTimeMs = Array.Empty<double>();
        CircleReadVtX = Array.Empty<double>();
        CircleHallX = Array.Empty<double>();
        CircleDiffX = Array.Empty<double>();
        CircleReadVtY = Array.Empty<double>();
        CircleHallY = Array.Empty<double>();
        CircleDiffY = Array.Empty<double>();
        SquareAxes = Array.Empty<SquareAxisSeries>();
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
        TiltUpTx = Array.Empty<double>();
        TiltUpTy = Array.Empty<double>();
        TiltDownTx = Array.Empty<double>();
        TiltDownTy = Array.Empty<double>();
        SinewaveTimeMs = Array.Empty<double>();
        SinewavePosiX = Array.Empty<double>();
        SinewaveHallX = Array.Empty<double>();
        SinewaveDiffX = Array.Empty<double>();
        SinewavePosiY = Array.Empty<double>();
        SinewaveHallY = Array.Empty<double>();
        SinewaveDiffY = Array.Empty<double>();
        CircleTimeMs = Array.Empty<double>();
        CircleReadVtX = Array.Empty<double>();
        CircleHallX = Array.Empty<double>();
        CircleDiffX = Array.Empty<double>();
        CircleReadVtY = Array.Empty<double>();
        CircleHallY = Array.Empty<double>();
        CircleDiffY = Array.Empty<double>();
        SquareAxes = Array.Empty<SquareAxisSeries>();
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
        TiltUpTx = Array.Empty<double>();
        TiltUpTy = Array.Empty<double>();
        TiltDownTx = Array.Empty<double>();
        TiltDownTy = Array.Empty<double>();
        SinewaveTimeMs = Array.Empty<double>();
        SinewavePosiX = Array.Empty<double>();
        SinewaveHallX = Array.Empty<double>();
        SinewaveDiffX = Array.Empty<double>();
        SinewavePosiY = Array.Empty<double>();
        SinewaveHallY = Array.Empty<double>();
        SinewaveDiffY = Array.Empty<double>();
        CircleTimeMs = Array.Empty<double>();
        CircleReadVtX = Array.Empty<double>();
        CircleHallX = Array.Empty<double>();
        CircleDiffX = Array.Empty<double>();
        CircleReadVtY = Array.Empty<double>();
        CircleHallY = Array.Empty<double>();
        CircleDiffY = Array.Empty<double>();
        SquareAxes = Array.Empty<SquareAxisSeries>();
    }

    public PlotFileItem(string fileName, string fullPath,
        IReadOnlyList<(double Tx, double Ty)> tiltUp,
        IReadOnlyList<(double Tx, double Ty)> tiltDown)
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
        SettlingGroups = Array.Empty<(string, IReadOnlyList<SettlingSeries>)>();
        IsTilt = true;
        TiltUpTx = tiltUp.Select(p => p.Tx).ToArray();
        TiltUpTy = tiltUp.Select(p => p.Ty).ToArray();
        TiltDownTx = tiltDown.Select(p => p.Tx).ToArray();
        TiltDownTy = tiltDown.Select(p => p.Ty).ToArray();
        SinewaveTimeMs = Array.Empty<double>();
        SinewavePosiX = Array.Empty<double>();
        SinewaveHallX = Array.Empty<double>();
        SinewaveDiffX = Array.Empty<double>();
        SinewavePosiY = Array.Empty<double>();
        SinewaveHallY = Array.Empty<double>();
        SinewaveDiffY = Array.Empty<double>();
        CircleTimeMs = Array.Empty<double>();
        CircleReadVtX = Array.Empty<double>();
        CircleHallX = Array.Empty<double>();
        CircleDiffX = Array.Empty<double>();
        CircleReadVtY = Array.Empty<double>();
        CircleHallY = Array.Empty<double>();
        CircleDiffY = Array.Empty<double>();
        SquareAxes = Array.Empty<SquareAxisSeries>();
    }

    public PlotFileItem(string fileName, string fullPath,
        IReadOnlyList<(double Time, double PosiX, double HallX, double DiffX, double PosiY, double HallY, double DiffY)> sinewaveSamples)
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
        SettlingGroups = Array.Empty<(string, IReadOnlyList<SettlingSeries>)>();
        TiltUpTx = Array.Empty<double>();
        TiltUpTy = Array.Empty<double>();
        TiltDownTx = Array.Empty<double>();
        TiltDownTy = Array.Empty<double>();
        IsSinewave = true;
        SinewaveTimeMs = sinewaveSamples.Select(s => s.Time).ToArray();
        SinewavePosiX = sinewaveSamples.Select(s => s.PosiX).ToArray();
        SinewaveHallX = sinewaveSamples.Select(s => s.HallX).ToArray();
        SinewaveDiffX = sinewaveSamples.Select(s => s.DiffX).ToArray();
        SinewavePosiY = sinewaveSamples.Select(s => s.PosiY).ToArray();
        SinewaveHallY = sinewaveSamples.Select(s => s.HallY).ToArray();
        SinewaveDiffY = sinewaveSamples.Select(s => s.DiffY).ToArray();
        CircleTimeMs = Array.Empty<double>();
        CircleReadVtX = Array.Empty<double>();
        CircleHallX = Array.Empty<double>();
        CircleDiffX = Array.Empty<double>();
        CircleReadVtY = Array.Empty<double>();
        CircleHallY = Array.Empty<double>();
        CircleDiffY = Array.Empty<double>();
        SquareAxes = Array.Empty<SquareAxisSeries>();
    }

    public PlotFileItem(string fileName, string fullPath, IReadOnlyList<CircleSample> circleSamples)
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
        SettlingGroups = Array.Empty<(string, IReadOnlyList<SettlingSeries>)>();
        TiltUpTx = Array.Empty<double>();
        TiltUpTy = Array.Empty<double>();
        TiltDownTx = Array.Empty<double>();
        TiltDownTy = Array.Empty<double>();
        SinewaveTimeMs = Array.Empty<double>();
        SinewavePosiX = Array.Empty<double>();
        SinewaveHallX = Array.Empty<double>();
        SinewaveDiffX = Array.Empty<double>();
        SinewavePosiY = Array.Empty<double>();
        SinewaveHallY = Array.Empty<double>();
        SinewaveDiffY = Array.Empty<double>();
        IsCircleTest = true;
        CircleTimeMs = circleSamples.Select(s => s.Time).ToArray();
        CircleReadVtX = circleSamples.Select(s => s.ReadVtX).ToArray();
        CircleHallX = circleSamples.Select(s => s.HallX).ToArray();
        CircleDiffX = circleSamples.Select(s => s.DiffX).ToArray();
        CircleReadVtY = circleSamples.Select(s => s.ReadVtY).ToArray();
        CircleHallY = circleSamples.Select(s => s.HallY).ToArray();
        CircleDiffY = circleSamples.Select(s => s.DiffY).ToArray();
        SquareAxes = Array.Empty<SquareAxisSeries>();
    }

    public PlotFileItem(string fileName, string fullPath, IReadOnlyList<SquareAxisSeries> squareAxes)
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
        SettlingGroups = Array.Empty<(string, IReadOnlyList<SettlingSeries>)>();
        TiltUpTx = Array.Empty<double>();
        TiltUpTy = Array.Empty<double>();
        TiltDownTx = Array.Empty<double>();
        TiltDownTy = Array.Empty<double>();
        SinewaveTimeMs = Array.Empty<double>();
        SinewavePosiX = Array.Empty<double>();
        SinewaveHallX = Array.Empty<double>();
        SinewaveDiffX = Array.Empty<double>();
        SinewavePosiY = Array.Empty<double>();
        SinewaveHallY = Array.Empty<double>();
        SinewaveDiffY = Array.Empty<double>();
        CircleTimeMs = Array.Empty<double>();
        CircleReadVtX = Array.Empty<double>();
        CircleHallX = Array.Empty<double>();
        CircleDiffX = Array.Empty<double>();
        CircleReadVtY = Array.Empty<double>();
        CircleHallY = Array.Empty<double>();
        CircleDiffY = Array.Empty<double>();
        IsSquareTest = true;
        SquareAxes = squareAxes;
    }
}
