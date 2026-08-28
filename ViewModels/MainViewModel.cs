using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Win32;
using JahwaInspector.Light.Services;

namespace JahwaInspector.Light.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private void ApplyJudgementToAll()
    {
        foreach (var r in FraResults)
        {
            var (j, reason) = JudgeRowBySpec(r);
            r.Judge = j;
            r.JudgeReason = reason;
        }

        var map = FraResults.ToDictionary(x => x.FileName, x => (x.Judge, x.JudgeReason));
        foreach (var pf in PlotFiles)
        {
            if (map.TryGetValue(pf.FileName, out var v))
            {
                pf.Judge = v.Judge;
                pf.JudgeReason = v.JudgeReason;
            }
            else
            {
                pf.Judge = "";
                pf.JudgeReason = "";
            }
        }

        RefreshPlotFilter();
    }

    private (string judge, string reason) JudgeRowBySpec(FraResultRow r)
    {
        var reasons = new List<string>();

        static double? ParseSpec(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)) return v;
            return null;
        }

        double? minGm = ParseSpec(FraSpec.GainMarginDb);
        double? minPm = ParseSpec(FraSpec.PhaseMarginDeg);
        double? minPm3 = ParseSpec(FraSpec.PhaseMarginMinus3dBDeg);

        if (minGm is double gmSpec)
        {
            if (r.GM is double gm)
            {
                if (gm < gmSpec) reasons.Add($"GM<{gmSpec:0.##}");
            }
            else reasons.Add("GM=N/A");
        }

        if (minPm is double pmSpec)
        {
            if (r.PM is double pm)
            {
                if (pm < pmSpec) reasons.Add($"PM<{pmSpec:0.##}");
            }
            else reasons.Add("PM=N/A");
        }

        if (minPm3 is double pm3Spec)
        {
            if (r.PM3 is double pm3)
            {
                if (pm3 < pm3Spec) reasons.Add($"PM(-3dB)<{pm3Spec:0.##}");
            }
            else reasons.Add("PM(-3dB)=N/A");
        }

        return reasons.Count == 0 ? ("PASS", "") : ("NG", string.Join(", ", reasons));
    }

    private void UpdateRecentFromRow(FraResultRow? row)
    {
        if (row is null) return;

        string gm = row.GM?.ToString("0.00") ?? "N/A";
        string pm = row.PM?.ToString("0.00") ?? "N/A";
        string gmFreq = row.GM_Freq?.ToString("0.00") ?? "N/A";
        string pmFreq = row.PM_Freq?.ToString("0.00") ?? "N/A";
        string gain10 = row.Gain10Hz?.ToString("0.00") ?? "N/A";
        string pm3 = row.PM3?.ToString("0.00") ?? "N/A";
        string pm3Freq = row.PM3_Freq?.ToString("0.00") ?? "N/A";
        string src = row.FileName;

        if (FraSpec.LastGm != gm) FraSpec.LastGm = gm;
        if (FraSpec.LastPm != pm) FraSpec.LastPm = pm;
        if (FraSpec.LastGmFreq != gmFreq) FraSpec.LastGmFreq = gmFreq;
        if (FraSpec.LastPmFreq != pmFreq) FraSpec.LastPmFreq = pmFreq;
        if (FraSpec.Last10HzGain != gain10) FraSpec.Last10HzGain = gain10;
        if (FraSpec.LastPm3 != pm3) FraSpec.LastPm3 = pm3;
        if (FraSpec.LastPm3Freq != pm3Freq) FraSpec.LastPm3Freq = pm3Freq;
        if (FraSpec.SourceFile != src) FraSpec.SourceFile = src;
    }

    private void UpdateRecentFromScanRow(ScanResultRow? row)
    {
        if (row is null) return;

        string hys1 = row.Hys1?.ToString("0.###") ?? "N/A";
        string li1 = row.Li1?.ToString("0.###") ?? "N/A";

        if (ScanSpec.LastRange1 != (string.IsNullOrWhiteSpace(row.Range1) ? "-" : row.Range1)) ScanSpec.LastRange1 = string.IsNullOrWhiteSpace(row.Range1) ? "-" : row.Range1;
        if (ScanSpec.LastHys1 != hys1) ScanSpec.LastHys1 = hys1;
        if (ScanSpec.LastLi1 != li1) ScanSpec.LastLi1 = li1;
        if (ScanSpec.LastJudge != (string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge)) ScanSpec.LastJudge = string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge;
        if (ScanSpec.LastReason != (string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason)) ScanSpec.LastReason = string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason;
        if (ScanSpec.SourceFile != row.FileName) ScanSpec.SourceFile = row.FileName;
    }

    private void UpdateRecentFromTiltRow(TiltResultRow? row)
    {
        if (row is null) return;

        string peakUp = row.PeakTiltUp?.ToString("0.###") ?? "N/A";
        string peakDown = row.PeakTiltDown?.ToString("0.###") ?? "N/A";

        if (TiltSpec.LastPeakUp != peakUp) TiltSpec.LastPeakUp = peakUp;
        if (TiltSpec.LastPeakDown != peakDown) TiltSpec.LastPeakDown = peakDown;
        if (TiltSpec.LastJudge != (string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge)) TiltSpec.LastJudge = string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge;
        if (TiltSpec.LastReason != (string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason)) TiltSpec.LastReason = string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason;
        if (TiltSpec.SourceFile != row.FileName) TiltSpec.SourceFile = row.FileName;
    }

    private void UpdateRecentFromSinewaveRow(SinewaveResultRow? row)
    {
        if (row is null) return;

        string maxErrX = row.MaxErrX?.ToString("0.###") ?? "N/A";
        string maxErrY = row.MaxErrY?.ToString("0.###") ?? "N/A";

        if (SinewaveSpec.LastMaxErrX != maxErrX) SinewaveSpec.LastMaxErrX = maxErrX;
        if (SinewaveSpec.LastMaxErrY != maxErrY) SinewaveSpec.LastMaxErrY = maxErrY;
        if (SinewaveSpec.LastJudge != (string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge)) SinewaveSpec.LastJudge = string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge;
        if (SinewaveSpec.LastReason != (string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason)) SinewaveSpec.LastReason = string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason;
        if (SinewaveSpec.SourceFile != row.FileName) SinewaveSpec.SourceFile = row.FileName;
    }

    private void UpdateRecentFromCircleRow(CircleResultRow? row)
    {
        if (row is null) return;

        string maxErrX = row.MaxErrX?.ToString("0.###") ?? "N/A";
        string maxErrY = row.MaxErrY?.ToString("0.###") ?? "N/A";

        if (CircleSpec.LastMaxErrX != maxErrX) CircleSpec.LastMaxErrX = maxErrX;
        if (CircleSpec.LastMaxErrY != maxErrY) CircleSpec.LastMaxErrY = maxErrY;
        if (CircleSpec.LastJudge != (string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge)) CircleSpec.LastJudge = string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge;
        if (CircleSpec.LastReason != (string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason)) CircleSpec.LastReason = string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason;
        if (CircleSpec.SourceFile != row.FileName) CircleSpec.SourceFile = row.FileName;
    }

    private void UpdateRecentFromSquareRow(SquareResultRow? row)
    {
        if (row is null) return;

        string settle = row.MaxSettlingMs?.ToString("0.###") ?? "N/A";
        string overshoot = row.MaxOvershoot?.ToString("0.###") ?? "N/A";
        string settleUp = row.MaxSettlingUpMs?.ToString("0.###") ?? "N/A";
        string overshootUp = row.MaxOvershootUp?.ToString("0.###") ?? "N/A";
        string settleDown = row.MaxSettlingDownMs?.ToString("0.###") ?? "N/A";
        string overshootDown = row.MaxOvershootDown?.ToString("0.###") ?? "N/A";

        if (SquareSpec.LastSettling != settle) SquareSpec.LastSettling = settle;
        if (SquareSpec.LastOvershoot != overshoot) SquareSpec.LastOvershoot = overshoot;
        if (SquareSpec.LastSettlingUp != settleUp) SquareSpec.LastSettlingUp = settleUp;
        if (SquareSpec.LastOvershootUp != overshootUp) SquareSpec.LastOvershootUp = overshootUp;
        if (SquareSpec.LastSettlingDown != settleDown) SquareSpec.LastSettlingDown = settleDown;
        if (SquareSpec.LastOvershootDown != overshootDown) SquareSpec.LastOvershootDown = overshootDown;
        if (SquareSpec.LastJudge != (string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge)) SquareSpec.LastJudge = string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge;
        if (SquareSpec.LastReason != (string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason)) SquareSpec.LastReason = string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason;
        if (SquareSpec.SourceFile != row.FileName) SquareSpec.SourceFile = row.FileName;
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); CancelCommand.RaiseCanExecuteChanged(); } }

    private double _progressValue;
    public double ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(); } }

    private string _importedFolder = "";
    public string ImportedFolder
    {
        get => _importedFolder;
        private set
        {
            _importedFolder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImportedFolder));
            OnPropertyChanged(nameof(ImportedFolderLabel));
            ExportCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasImportedFolder => !string.IsNullOrWhiteSpace(ImportedFolder);
    public string ImportedFolderLabel => HasImportedFolder ? $"Folder: {Path.GetFileName(ImportedFolder)}" : "Folder: (not selected)";

    private string _exportOutputLabel = "";
    public string ExportOutputLabel { get => _exportOutputLabel; set { _exportOutputLabel = value; OnPropertyChanged(); } }

    private string _rawMergedLabel = "";
    public string RawMergedLabel { get => _rawMergedLabel; set { _rawMergedLabel = value; OnPropertyChanged(); } }

    public sealed record FraPhaseOption(string Name, bool IsInverted)
    {
        public override string ToString() => Name;
    }

    public ObservableCollection<string> InspectionOptions { get; } = new() { "FRA", "DC SENSITIVITY", "SCAN", "SETTLING TIME", "TILT", "SINE WAVE", "CIRCLE TEST", "SQUARE TEST" };
    public ObservableCollection<string> FraModeOptions { get; } = new() { "OPEN LOOP", "PLANT" };
    public ObservableCollection<IFraDataTypeHandler> FraDataTypeOptions { get; } =
        new(FraDataTypeRegistry.All);
    public ObservableCollection<FraPhaseOption> FraPhaseOptions { get; } = new()
    {
        new("Non-Inverted", false),
        new("Inverted", true),
    };

    // 설비사 목록은 SettlingDataTypeRegistry 한 곳에서만 관리한다.
    // 다른 검사(SCAN/TILT/SINE/CIRCLE/SQUARE/DC)와 동일한 방식.
    public sealed record SettlingVendorOption(ISettlingDataTypeHandler Handler)
    {
        public string Name => Handler.Name;
        public override string ToString() => Handler.Name;
    }

    public ObservableCollection<SettlingVendorOption> SettlingVendorOptions { get; } =
        new(SettlingDataTypeRegistry.All.Select(h => new SettlingVendorOption(h)));

    private SettlingVendorOption? _selectedSettlingVendorOption;
    public SettlingVendorOption? SelectedSettlingVendorOption
    {
        get => _selectedSettlingVendorOption;
        set { _selectedSettlingVendorOption = value; OnPropertyChanged(); }
    }


    private string? _selectedInspection;
    public string? SelectedInspection { get => _selectedInspection; set { _selectedInspection = value; OnPropertyChanged(); UpdateSpecPanel(); RaiseTab(); } }

    private string? _selectedFraMode;
    public string? SelectedFraMode { get => _selectedFraMode; set { _selectedFraMode = value; OnPropertyChanged(); } }

    private IFraDataTypeHandler? _selectedFraDataType;
    public IFraDataTypeHandler? SelectedFraDataType
    {
        get => _selectedFraDataType;
        set
        {
            _selectedFraDataType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FraHeaderDescription));
            OnPropertyChanged(nameof(FraFrequencyHeaders));
            OnPropertyChanged(nameof(FraGainHeaders));
            OnPropertyChanged(nameof(FraPhaseHeaders));
        }
    }

    public string FraHeaderDescription =>
        SelectedFraDataType?.HeaderDescription ?? "Data Type을 선택하세요.";

    public string FraFrequencyHeaders =>
        SelectedFraDataType is null ? "-" : string.Join(" / ", SelectedFraDataType.FrequencyHeaders);

    public string FraGainHeaders =>
        SelectedFraDataType is null ? "-" : string.Join(" / ", SelectedFraDataType.GainHeaders);

    public string FraPhaseHeaders =>
        SelectedFraDataType is null ? "-" : string.Join(" / ", SelectedFraDataType.PhaseHeaders);

    private FraPhaseOption? _selectedFraPhaseOption;
    public FraPhaseOption? SelectedFraPhaseOption
    {
        get => _selectedFraPhaseOption;
        set
        {
            _selectedFraPhaseOption = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFraPhaseInverted));
            if (!IsBusy && FraResults.Count > 0)
                RecalculateFraResultsFromCurrentPlotData();
        }
    }

    public bool IsFraPhaseInverted => SelectedFraPhaseOption?.IsInverted ?? false;

    public EmptySpecViewModel EmptySpec { get; } = new();
    public FraSpecViewModel FraSpec { get; } = new();
    public DcSensitivitySpecViewModel DcSensitivitySpec { get; } = new();
    public ScanSpecViewModel ScanSpec { get; } = new();
    public SettlingSpecViewModel SettlingSpec { get; } = new();
    public TiltSpecViewModel TiltSpec { get; } = new();
    public SinewaveSpecViewModel SinewaveSpec { get; } = new();
    public CircleTestSpecViewModel CircleSpec { get; } = new();
    public SquareTestSpecViewModel SquareSpec { get; } = new();

    private ViewModelBase _activeSpec;
    public ViewModelBase ActiveSpec { get => _activeSpec; private set { _activeSpec = value; OnPropertyChanged(); } }

    private bool _isPlotTab = true;
    public bool IsPlotTab { get => _isPlotTab; set { if (_isPlotTab == value) return; _isPlotTab = value; if (value) { _isTableTab = false; _isRawTab = false; } RaiseTab(); } }

    private bool _isTableTab;
    public bool IsTableTab { get => _isTableTab; set { if (_isTableTab == value) return; _isTableTab = value; if (value) { _isPlotTab = false; _isRawTab = false; } RaiseTab(); } }

    private bool _isRawTab;
    public bool IsRawTab { get => _isRawTab; set { if (_isRawTab == value) return; _isRawTab = value; if (value) { _isPlotTab = false; _isTableTab = false; } RaiseTab(); } }

    private void RaiseTab()
    {
        OnPropertyChanged(nameof(IsPlotTab));
        OnPropertyChanged(nameof(IsTableTab));
        OnPropertyChanged(nameof(IsRawTab));
        OnPropertyChanged(nameof(ActiveTabTitle));
        OnPropertyChanged(nameof(ActiveTabSubtitle));
        OnPropertyChanged(nameof(IsFraInspection));
        OnPropertyChanged(nameof(IsDcSensitivityInspection));
        OnPropertyChanged(nameof(IsScanInspection));
        OnPropertyChanged(nameof(IsSettlingInspection));
        OnPropertyChanged(nameof(IsTiltInspection));
        OnPropertyChanged(nameof(IsSinewaveInspection));
        OnPropertyChanged(nameof(IsCircleInspection));
        OnPropertyChanged(nameof(IsSquareInspection));
    }

    public string ActiveTabTitle => IsPlotTab ? "Plot" : IsTableTab ? "Table" : "Raw";
    public string ActiveTabSubtitle => SelectedInspection switch
    {
        "DC SENSITIVITY" => IsPlotTab
            ? "파일별 Up/Down 오버레이 플롯"
            : IsTableTab
                ? "파일별 컬럼 매칭 및 분리 결과"
                : "통합 CSV 미리보기 (열 순서: up/down 반복)",
        "SCAN" => IsPlotTab
            ? "파일별 Up/Down + Reference 라인"
            : IsTableTab
                ? "파일별 Hys / LI 계산 결과"
                : "SCAN 요약 미리보기",
        "SETTLING TIME" => IsPlotTab
            ? "파일별 Step 응답 시계열 + Target/Tolerance 참조선"
            : IsTableTab
                ? "그룹/축별 Settling Time · Overshoot 판정 결과"
                : "SETTLING TIME 요약 미리보기",
        "TILT" => IsPlotTab
            ? "파일별 Tx-Ty 4분면 플롯 (Up/Down)"
            : IsTableTab
                ? "파일별 Peak Tilt 판정 결과"
                : "TILT 요약 미리보기",
        "SINE WAVE" => IsPlotTab
            ? "파일별 Posi/Hall 오버레이 + Diff 시계열"
            : IsTableTab
                ? "파일별 MaxErr 판정 결과"
                : "SINE WAVE 요약 미리보기",
        "CIRCLE TEST" => IsPlotTab
            ? "파일별 ReadVt/Hall 오버레이 (X/Y)"
            : IsTableTab
                ? "파일별 MaxErr 판정 결과"
                : "CIRCLE TEST 요약 미리보기",
        "SQUARE TEST" => IsPlotTab
            ? "파일별 Target/Value 오버레이 (스텝 응답)"
            : IsTableTab
                ? "파일별 Up/Down Settling/Overshoot 판정 결과"
                : "SQUARE TEST 요약 미리보기",
        _ => IsPlotTab
            ? $"전체 FRA Bode Plot 오버레이 ({SelectedFraMode ?? "Open loop"})"
            : IsTableTab
                ? $"FRA 요약 지표 ({SelectedFraMode ?? "Open loop"})"
                : $"파일별 Raw 3열(F/G/P) 원본 병합 ({SelectedFraMode ?? "Open loop"})"
    };

    public bool IsFraInspection => SelectedInspection == "FRA";
    public bool IsDcSensitivityInspection => SelectedInspection == "DC SENSITIVITY";
    public bool IsScanInspection => SelectedInspection == "SCAN";
    public bool IsSettlingInspection => SelectedInspection == "SETTLING TIME";
    public bool IsTiltInspection => SelectedInspection == "TILT";
    public bool IsSinewaveInspection => SelectedInspection == "SINE WAVE";
    public bool IsCircleInspection => SelectedInspection == "CIRCLE TEST";
    public bool IsSquareInspection => SelectedInspection == "SQUARE TEST";

    private bool _isEmpty = true;
    public bool IsEmpty { get => _isEmpty; set { _isEmpty = value; OnPropertyChanged(); } }

    public ObservableCollection<FraResultRow> FraResults { get; } = new();
    public ObservableCollection<DcSensitivityResultRow> DcResults { get; } = new();
    public ObservableCollection<ScanResultRow> ScanResults { get; } = new();
    public ObservableCollection<SettlingResultRow> SettlingResults { get; } = new();
    public ObservableCollection<TiltResultRow> TiltResults { get; } = new();
    public ObservableCollection<SinewaveResultRow> SinewaveResults { get; } = new();
    public ObservableCollection<CircleResultRow> CircleResults { get; } = new();
    public ObservableCollection<SquareResultRow> SquareResults { get; } = new();

    private FraResultRow? _selectedFraRow;
    public FraResultRow? SelectedFraRow
    {
        get => _selectedFraRow;
        set
        {
            _selectedFraRow = value;
            OnPropertyChanged();
            if (_selectedFraRow is not null) UpdateRecentFromRow(_selectedFraRow);
            if (IsFraInspection) SyncSelectionToPlot();
        }
    }

    private DcSensitivityResultRow? _selectedDcRow;
    public DcSensitivityResultRow? SelectedDcRow
    {
        get => _selectedDcRow;
        set
        {
            _selectedDcRow = value;
            OnPropertyChanged();
            if (IsDcSensitivityInspection) SyncSelectionToPlot();
        }
    }

    private ScanResultRow? _selectedScanRow;
    public ScanResultRow? SelectedScanRow
    {
        get => _selectedScanRow;
        set
        {
            _selectedScanRow = value;
            OnPropertyChanged();
            if (_selectedScanRow is not null) UpdateRecentFromScanRow(_selectedScanRow);
            if (IsScanInspection) SyncSelectionToPlot();
        }
    }

    private SettlingResultRow? _selectedSettlingRow;
    public SettlingResultRow? SelectedSettlingRow
    {
        get => _selectedSettlingRow;
        set
        {
            _selectedSettlingRow = value;
            OnPropertyChanged();
            if (_selectedSettlingRow is not null) UpdateRecentFromSettlingRow(_selectedSettlingRow);
            if (IsSettlingInspection) SyncSelectionToPlot();
        }
    }

    private TiltResultRow? _selectedTiltRow;
    public TiltResultRow? SelectedTiltRow
    {
        get => _selectedTiltRow;
        set
        {
            _selectedTiltRow = value;
            OnPropertyChanged();
            if (_selectedTiltRow is not null) UpdateRecentFromTiltRow(_selectedTiltRow);
            if (IsTiltInspection) SyncSelectionToPlot();
        }
    }

    private SinewaveResultRow? _selectedSinewaveRow;
    public SinewaveResultRow? SelectedSinewaveRow
    {
        get => _selectedSinewaveRow;
        set
        {
            _selectedSinewaveRow = value;
            OnPropertyChanged();
            if (_selectedSinewaveRow is not null) UpdateRecentFromSinewaveRow(_selectedSinewaveRow);
            if (IsSinewaveInspection) SyncSelectionToPlot();
        }
    }

    private CircleResultRow? _selectedCircleRow;
    public CircleResultRow? SelectedCircleRow
    {
        get => _selectedCircleRow;
        set
        {
            _selectedCircleRow = value;
            OnPropertyChanged();
            if (_selectedCircleRow is not null) UpdateRecentFromCircleRow(_selectedCircleRow);
            if (IsCircleInspection) SyncSelectionToPlot();
        }
    }

    private SquareResultRow? _selectedSquareRow;
    public SquareResultRow? SelectedSquareRow
    {
        get => _selectedSquareRow;
        set
        {
            _selectedSquareRow = value;
            OnPropertyChanged();
            if (_selectedSquareRow is not null) UpdateRecentFromSquareRow(_selectedSquareRow);
            if (IsSquareInspection) SyncSelectionToPlot();
        }
    }

    public ObservableCollection<PlotFileItem> PlotFiles { get; } = new();
    public ObservableCollection<PlotFileItem> PlotFilesFiltered { get; } = new();

    private string _plotSearchText = "";
    public string PlotSearchText
    {
        get => _plotSearchText;
        set { _plotSearchText = value; OnPropertyChanged(); RefreshPlotFilter(); }
    }

    private PlotFileItem? _selectedPlotFile;
    public PlotFileItem? SelectedPlotFile
    {
        get => _selectedPlotFile;
        set
        {
            _selectedPlotFile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActivePlotFile));
            OnPropertyChanged(nameof(PlotTitle));
            SyncSelectionToTable();
            SaveSelectedSettlingImagesCommand.RaiseCanExecuteChanged();
        }
    }

    public PlotFileItem? ActivePlotFile => SelectedPlotFile is { IsAllOverlayItem: true } ? null : SelectedPlotFile;
    public string PlotTitle => SelectedPlotFile is { IsAllOverlayItem: true }
        ? "All Overlay"
        : ActivePlotFile is null
            ? "All Files"
            : $"Selected: {ActivePlotFile.FileName}";
    public int SelectedOverlayCount => PlotFiles.Count(x => x.IsOverlaySelected && x.Points.Count > 0);
    public bool CanSaveSelectedFraOverlay => IsFraInspection && SelectedOverlayCount > 0;

    // 검사항목과 무관하게 "체크된 파일" 개수. FRA 전용 SelectedOverlayCount와 달리
    // Points(=FRA 데이터) 유무를 따지지 않는다.
    public int SelectedPlotCount => PlotFiles.Count(x => x.IsOverlaySelected && !x.IsAllOverlayItem);
    public bool CanSaveSelectedPlotImages => SelectedPlotCount > 0;
    public bool CanSaveSelectedDcOverlay => IsDcSensitivityInspection && SelectedPlotCount > 0;

    private string _selectedOverlayExportLabel = "";
    public string SelectedOverlayExportLabel
    {
        get => _selectedOverlayExportLabel;
        set { _selectedOverlayExportLabel = value; OnPropertyChanged(); }
    }

    public const string FraHoverInfoEmpty = "Freq: - | Gain: - | Phase: -";

    private string _fraHoverFrequencyLabel = FraHoverInfoEmpty;
    public string FraHoverFrequencyLabel
    {
        get => _fraHoverFrequencyLabel;
        set { _fraHoverFrequencyLabel = value; OnPropertyChanged(); }
    }

    private DataView? _rawPreview;
    public DataView? RawPreview { get => _rawPreview; private set { _rawPreview = value; OnPropertyChanged(); } }

    public RelayCommand ImportCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SaveSelectedFraOverlayCommand { get; }
    public RelayCommand SaveSelectedSettlingImagesCommand { get; }
    // 검사항목 공통: 체크한 파일을 파일당 개별 PNG로 저장
    public RelayCommand SaveSelectedPlotImagesCommand { get; }
    // DC SENSITIVITY 전용: 체크한 파일 전체를 한 장에 겹쳐 저장
    public RelayCommand SaveSelectedDcOverlayCommand { get; }

    public MainViewModel()
    {
        _activeSpec = EmptySpec;
        SelectedInspection = "FRA";
        SelectedFraMode = FraModeOptions.FirstOrDefault();
        SelectedFraDataType = FraDataTypeOptions.FirstOrDefault();
        SelectedFraPhaseOption = FraPhaseOptions.FirstOrDefault();
        SelectedSettlingVendorOption = SettlingVendorOptions.FirstOrDefault();

        ImportCommand = new RelayCommand(ImportFolder);
        ExportCommand = new RelayCommand(ExportBatch, () => HasImportedFolder);
        CancelCommand = new RelayCommand(() => { IsBusy = false; ProgressValue = 0; }, () => IsBusy);
        SaveSelectedFraOverlayCommand = new RelayCommand(SaveSelectedFraOverlay, () => IsFraInspection && PlotFiles.Any(x => x.IsOverlaySelected && x.Points.Count > 0));
        // SETTLING도 다른 항목과 동일하게 "체크한 파일 전부"를 대상으로 한다.
        SaveSelectedSettlingImagesCommand = new RelayCommand(SaveSelectedPlotImages, () => IsSettlingInspection && CanSaveSelectedPlotImages);
        SaveSelectedPlotImagesCommand = new RelayCommand(SaveSelectedPlotImages, () => CanSaveSelectedPlotImages);
        SaveSelectedDcOverlayCommand = new RelayCommand(SaveSelectedDcOverlay, () => CanSaveSelectedDcOverlay);

        FraSpec.PropertyChanged += (_, e) =>
        {
            if (!IsBusy && FraResults.Count > 0 && IsFraJudgementSpecProperty(e.PropertyName))
                ApplyJudgementToAll();
        };

        ScanSpec.PropertyChanged += (_, e) =>
        {
            if (!IsBusy && ScanResults.Count > 0 && IsScanJudgementSpecProperty(e.PropertyName))
                ApplyScanJudgementToAll();
        };

        TiltSpec.PropertyChanged += (_, e) =>
        {
            if (!IsBusy && TiltResults.Count > 0 && IsTiltJudgementSpecProperty(e.PropertyName))
                ApplyTiltJudgementToAll();
        };

        SinewaveSpec.PropertyChanged += (_, e) =>
        {
            if (!IsBusy && SinewaveResults.Count > 0 && IsSinewaveJudgementSpecProperty(e.PropertyName))
                ApplySinewaveJudgementToAll();
        };

        CircleSpec.PropertyChanged += (_, e) =>
        {
            if (!IsBusy && CircleResults.Count > 0 && IsCircleJudgementSpecProperty(e.PropertyName))
                ApplyCircleJudgementToAll();
        };

        SquareSpec.PropertyChanged += (_, e) =>
        {
            if (!IsBusy && SquareResults.Count > 0 && IsSquareJudgementSpecProperty(e.PropertyName))
                ApplySquareJudgementToAll();
        };

        SettlingSpec.PropertyChanged += (_, e) =>
        {
            if (IsBusy) return;
            if (IsSettlingRecalculationSpecProperty(e.PropertyName) && PlotFiles.Any(p => p.IsSettling))
                RecalculateSettlingResultsFromCurrentPlotData();
            else if (IsSettlingJudgementSpecProperty(e.PropertyName) && SettlingResults.Count > 0)
                ApplySettlingJudgementToAll();
        };

        void OnSettlingConditionToleranceEdited(object? _, PropertyChangedEventArgs __)
        {
            if (IsBusy) return;
            if (PlotFiles.Any(p => p.IsSettling))
                RecalculateSettlingResultsFromCurrentPlotData();
        }

        SettlingSpec.ConditionTolerances.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (SettlingConditionTolerance item in e.NewItems)
                    item.PropertyChanged += OnSettlingConditionToleranceEdited;
        };

        // 체크박스(IsOverlaySelected) 변경을 감지하려면 항목마다 PropertyChanged를 구독해야 한다.
        // 예전에는 FRA를 채우는 경로에서만 구독해서, 다른 검사항목은 체크해도 버튼이 활성화되지 않았다.
        // 개별 Add 지점에 흩어놓지 않고 컬렉션에 들어오는 시점에 한 번에 구독한다.
        PlotFiles.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
                foreach (PlotFileItem item in e.OldItems)
                    item.PropertyChanged -= PlotItemOnPropertyChanged;

            if (e.NewItems is not null)
                foreach (PlotFileItem item in e.NewItems)
                {
                    item.PropertyChanged -= PlotItemOnPropertyChanged;   // 중복 구독 방지
                    item.PropertyChanged += PlotItemOnPropertyChanged;
                }

            RefreshSelectedPlotCommands();
        };

        RefreshPlotFilter();
    }

    private void ImportFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Import: CSV 폴더 선택",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        ImportedFolder = dlg.SelectedPath;
        SettlingSpec.ConditionTolerances.Clear();
        ResetResults();
    }

    private void ResetResults()
    {
        IsEmpty = true;
        FraResults.Clear();
        DcResults.Clear();
        ScanResults.Clear();
        SettlingResults.Clear();
        TiltResults.Clear();
        SinewaveResults.Clear();
        CircleResults.Clear();
        SquareResults.Clear();
        PlotFiles.Clear();
        PlotFilesFiltered.Clear();
        SelectedPlotFile = null;
        SelectedFraRow = null;
        SelectedDcRow = null;
        SelectedScanRow = null;
        SelectedSettlingRow = null;
        SelectedTiltRow = null;
        SelectedSinewaveRow = null;
        SelectedCircleRow = null;
        SelectedSquareRow = null;
        RawPreview = null;
        ExportOutputLabel = "";
        RawMergedLabel = "";
        FraHoverFrequencyLabel = FraHoverInfoEmpty;
        RefreshSelectedPlotCommands();
    }

    // 체크 상태/목록이 바뀔 때 관련 표시값과 버튼 활성화를 한꺼번에 갱신한다.
    // RelayCommand가 CommandManager를 쓰지 않으므로 반드시 명시적으로 호출해야 한다.
    private void RefreshSelectedPlotCommands()
    {
        OnPropertyChanged(nameof(SelectedOverlayCount));
        OnPropertyChanged(nameof(CanSaveSelectedFraOverlay));
        OnPropertyChanged(nameof(SelectedPlotCount));
        OnPropertyChanged(nameof(CanSaveSelectedPlotImages));
        OnPropertyChanged(nameof(CanSaveSelectedDcOverlay));
        SaveSelectedFraOverlayCommand.RaiseCanExecuteChanged();
        SaveSelectedPlotImagesCommand.RaiseCanExecuteChanged();
        SaveSelectedDcOverlayCommand.RaiseCanExecuteChanged();
        SaveSelectedSettlingImagesCommand.RaiseCanExecuteChanged();
    }

    private void ExportBatch()
    {
        if (!HasImportedFolder) return;

        switch (SelectedInspection)
        {
            case "FRA":
                ExportFraBatch();
                break;
            case "DC SENSITIVITY":
                ExportDcSensitivityBatch();
                break;
            case "SCAN":
                ExportScanBatch();
                break;
            case "SETTLING TIME":
                ExportSettlingBatch();
                break;
            case "TILT":
                ExportTiltBatch();
                break;
            case "SINE WAVE":
                ExportSinewaveBatch();
                break;
            case "CIRCLE TEST":
                ExportCircleBatch();
                break;
            case "SQUARE TEST":
                ExportSquareBatch();
                break;
            default:
                ExportOutputLabel = "해당 검사 항목은 아직 구현되지 않았습니다.";
                break;
        }
    }

    private void ExportFraBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("FRA", files))
            return;

        if (!ConfirmExportDialog("FRA", "FRA", SelectedFraDataType?.Name ?? "AUTO", files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        var failures = new List<string>();

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "FRA");
            Directory.CreateDirectory(outDir);

            var dataType = SelectedFraDataType ?? FraDataTypeRegistry.Get(null);
            var loaded = new List<(string file, List<BodePoint> rawPts, List<BodePoint> processedPts, BodeMetricsResult m, double? g10)>();

            PlotFiles.Add(new PlotFileItem("ALL OVERLAY", "__ALL_OVERLAY__", Array.Empty<BodePoint>(), isAllOverlayItem: true));

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    // 파일별로 격리한다. 예전에는 try/catch가 없어서 파일 하나가 실패하면
                    // 나머지가 멀쩡해도 Export 전체가 중단됐다.
                    var rawPts = BodeCsvReader.Read(file, dataType);

                    // BodeCsvReader는 헤더를 못 찾으면 예외 대신 1/2/3열 폴백을 쓴다.
                    // 그래서 형식이 다른 파일은 예외 없이 0건으로 조용히 통과해버린다 — 실패로 잡는다.
                    if (rawPts.Count == 0)
                        throw new InvalidOperationException("유효한 Frequency/Gain/Phase 데이터 행을 찾지 못했습니다.");

                    var processedPts = ApplyFraPhaseMode(rawPts);
                    var m = BodeMetrics.Extract(processedPts, useCrossGuard: false);
                    var g10 = FraHelpers.GainNearestAtHz(rawPts, 10.0);

                    loaded.Add((file, rawPts, processedPts, m, g10));
                    // PropertyChanged 구독은 PlotFiles.CollectionChanged 에서 일괄 처리한다.
                    PlotFiles.Add(new PlotFileItem(Path.GetFileName(file), file, rawPts));
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 30.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();

            foreach (var it in loaded)
            {
                FraResults.Add(new FraResultRow
                {
                    FileName = Path.GetFileName(it.file),
                    Gain10Hz = it.g10,
                    GM = it.m.GainMarginDb,
                    GM_Freq = it.m.GmFreqHz,
                    PM = it.m.PhaseMarginDeg,
                    PM_Freq = it.m.PmFreqHz,
                    PM3 = it.m.PhaseMarginAtMinus3dBDeg,
                    PM3_Freq = it.m.PmMinus3dBFreqHz,
                });
            }

            ApplyJudgementToAll();
            ProgressValue = 50;

            var summaryPath = Path.Combine(outDir, "FRA_Summary.csv");
            using (var sw = new StreamWriter(summaryPath))
            {
                sw.WriteLine("FileName,Judge,Reason,Gain10Hz,GM,GM_Freq,PM,PM_Freq,PM3,PM3_Freq");
                foreach (var r in FraResults)
                {
                    sw.WriteLine(string.Join(",",
                        Esc(r.FileName),
                        Esc(r.Judge),
                        Esc(r.JudgeReason),
                        r.Gain10Hz?.ToString("0.00") ?? "",
                        r.GM?.ToString("0.00") ?? "",
                        r.GM_Freq?.ToString("0.00") ?? "",
                        r.PM?.ToString("0.00") ?? "",
                        r.PM_Freq?.ToString("0.00") ?? "",
                        r.PM3?.ToString("0.00") ?? "",
                        r.PM3_Freq?.ToString("0.00") ?? ""
                    ));
                }
            }

            if (FraSpec.SavePlotImage && loaded.Count > 0)
            {
                var plotDir = Path.Combine(outDir, "Plots");
                Directory.CreateDirectory(plotDir);
                foreach (var x in loaded)
                {
                    var baseName = Path.GetFileNameWithoutExtension(x.file);
                    foreach (var c in Path.GetInvalidFileNameChars())
                        baseName = baseName.Replace(c, '_');
                    var pngPath = Path.Combine(plotDir, $"{baseName}.png");
                    PlotImageExporter.SaveBodeOverlayPng(
                        new List<(string name, IReadOnlyList<BodePoint> pts)> { (baseName, x.processedPts) },
                        pngPath,
                        width: 1400,
                        height: 900);
                }

                var allOverlayPath = Path.Combine(plotDir, "FRA_All_Overlay.png");
                PlotImageExporter.SaveBodeOverlayPng(
                    loaded.Select(x => (Path.GetFileNameWithoutExtension(x.file), (IReadOnlyList<BodePoint>)x.processedPts)).ToList(),
                    allOverlayPath,
                    width: 1600,
                    height: 1000,
                    fixedLineWidth: 2.4f,
                    fixedOpacity: 0.95);

                ExportOutputLabel = $"Output: {outDir}\nPlot PNGs: {plotDir}\n- 전체 오버레이: {Path.GetFileName(allOverlayPath)}";
            }

            ProgressValue = 65;
            var mergedPath = Path.Combine(outDir, "FRA_RawMerged.csv");
            if (loaded.Count > 0)
            {
                int maxRows = loaded.Max(x => x.processedPts.Count);
                var dt = new DataTable();

                dt.Columns.Add("Frequency", typeof(double));
                for (int i = 0; i < loaded.Count; i++)
                {
                    var it = loaded[i];
                    var name = Path.GetFileNameWithoutExtension(it.file);
                    dt.Columns.Add($"Gain_{name}", typeof(double));
                    dt.Columns.Add($"Phase_{name}", typeof(double));
                }

                for (int r = 0; r < maxRows; r++)
                {
                    var row = dt.NewRow();
                    row[0] = r < loaded[0].processedPts.Count ? loaded[0].processedPts[r].FrequencyHz : DBNull.Value;
                    int col = 1;
                    foreach (var it in loaded)
                    {
                        if (r < it.processedPts.Count)
                        {
                            row[col++] = it.processedPts[r].GainDb;
                            row[col++] = it.processedPts[r].PhaseDeg;
                        }
                        else
                        {
                            row[col++] = DBNull.Value;
                            row[col++] = DBNull.Value;
                        }
                    }
                    dt.Rows.Add(row);
                }

                SaveDataTableToCsv(dt, mergedPath);
                RawPreview = TakePreview(dt, 30).DefaultView;
                RawMergedLabel = $"Merged CSV saved: {mergedPath} (원본 Raw 기준으로 병합)";
            }
            else
            {
                RawMergedLabel = "No CSV files found.";
                RawPreview = null;
            }

            ProgressValue = 85;
            if (FraResults.Count > 0)
            {
                SelectedFraRow = FraResults[0];
                UpdateRecentFromRow(FraResults[0]);
            }

            if (!FraSpec.SavePlotImage)
                ExportOutputLabel = $"Output folder: {outDir}\n- FRA_Summary.csv\n- FRA_RawMerged.csv";

            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "FRA_ErrorLog.txt");
                SaveExportFailureLog(errorLogPath, "FRA", SelectedFraDataType?.Name ?? "AUTO",
                    failures, files.Length, FraResults.Count);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowExportFailureMessage("FRA", failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            ShowExportExceptionMessage("FRA", ex);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExportDcSensitivityBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*.csv", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("DC SENSITIVITY", files))
            return;

        if (!ConfirmExportDialog("DC SENSITIVITY", "DC sensitivity", DcSensitivitySpec.SelectedDataType, files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "DC sensitivity");
            Directory.CreateDirectory(outDir);
            var plotDir = Path.Combine(outDir, "Plots");
            if (DcSensitivitySpec.SavePlotImage)
                Directory.CreateDirectory(plotDir);

            var inputCandidates = SplitCandidates(DcSensitivitySpec.InputCandidatesText);
            var outputCandidates = SplitCandidates(DcSensitivitySpec.OutputCandidatesText);
            var dataType = DcSensitivityDataTypeRegistry.Get(DcSensitivitySpec.SelectedDataType);
            var combined = new List<(string FileStem, IReadOnlyList<double> UpOutput, IReadOnlyList<double> DownOutput)>();
            var summaryPlots = new List<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)>();
            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var read = dataType.Read(file, inputCandidates, outputCandidates, Math.Max(1, DcSensitivitySpec.HeaderScanRows));
                    var stem = Path.GetFileNameWithoutExtension(file);
                    string plotPath = "";

                    if (DcSensitivitySpec.SavePlotImage)
                    {
                        plotPath = Path.Combine(plotDir, $"{SanitizeFileName(stem)}_plot.png");
                        SaveDcPlotPng(read.UpInput, read.UpOutput, read.DownInput, read.DownOutput, plotPath, stem, read.InputColumn, read.OutputColumn);
                    }

                    DcResults.Add(new DcSensitivityResultRow
                    {
                        FileName = Path.GetFileName(file),
                        HeaderRow = read.HeaderRow,
                        InputColumn = read.InputColumn,
                        OutputColumn = read.OutputColumn,
                        UpCount = read.UpOutput.Count,
                        DownCount = read.DownOutput.Count,
                        PlotPath = plotPath,
                    });

                    PlotFiles.Add(new PlotFileItem(Path.GetFileName(file), file, read.UpInput, read.UpOutput, read.DownInput, read.DownOutput));
                    combined.Add((stem, read.UpOutput, read.DownOutput));
                    if (!string.IsNullOrWhiteSpace(plotPath))
                        summaryPlots.Add((Path.GetFileName(file), plotPath, read.HeaderRow, read.InputColumn, read.OutputColumn, read.UpOutput.Count, read.DownOutput.Count));
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 75.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();

            string? summaryExcelPath = null;
            if (DcSensitivitySpec.SaveCombinedCsv && combined.Count > 0)
            {
                var dt = DcSensitivityCsvReader.BuildCombinedOutputOnlyTable(combined);
                var combinedPath = Path.Combine(outDir, "DC_Sensitivity_Raw.csv");
                SaveDataTableToCsv(dt, combinedPath);
                RawPreview = TakePreview(dt, 30).DefaultView;
                RawMergedLabel = $"Combined CSV saved: {combinedPath}";
            }
            else
            {
                RawPreview = null;
                RawMergedLabel = combined.Count == 0 ? "처리할 CSV가 없습니다." : "Combined CSV 저장 옵션이 꺼져 있습니다.";
            }

            if (DcSensitivitySpec.SaveSummaryExcel && summaryPlots.Count > 0)
            {
                summaryExcelPath = Path.Combine(outDir, "DC_Sensitivity_Summary.xlsx");
                SaveDcSummaryExcel(summaryExcelPath, summaryPlots);
            }

            SelectedDcRow = DcResults.FirstOrDefault();
            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            var summary = new StringBuilder();
            summary.AppendLine($"Output folder: {outDir}");
            if (DcSensitivitySpec.SavePlotImage)
                summary.AppendLine($"- Plot PNGs: {plotDir}");
            if (DcSensitivitySpec.SaveCombinedCsv)
                summary.AppendLine("- DC_Sensitivity_Raw.csv");
            if (DcSensitivitySpec.SaveSummaryExcel && !string.IsNullOrWhiteSpace(summaryExcelPath))
                summary.AppendLine("- DC_Sensitivity_Summary.xlsx");
            if (failures.Count > 0)
                summary.AppendLine($"- 실패 {failures.Count}건 (아래 로그 확인)");
            ExportOutputLabel = summary.ToString().TrimEnd();
            DcSensitivitySpec.LastSummary = $"Success {DcResults.Count} / Failed {failures.Count}";
            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "DC_Sensitivity_ErrorLog.txt");
                SaveExportFailureLog(errorLogPath, "DC SENSITIVITY", DcSensitivitySpec.SelectedDataType,
                    failures, files.Length, DcResults.Count);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowExportFailureMessage("DC SENSITIVITY", failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            ShowExportExceptionMessage("DC SENSITIVITY", ex);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExportScanBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("SCAN", files))
            return;

        if (!ConfirmScanExport(files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "SCAN");
            Directory.CreateDirectory(outDir);
            var plotDir = Path.Combine(outDir, "Plots");
            if (ScanSpec.SavePlotImage)
                Directory.CreateDirectory(plotDir);

            var inputCandidates = SplitCandidates(ScanSpec.InputCandidatesText);
            var outputCandidates = SplitCandidates(ScanSpec.OutputCandidatesText);
            var configuredRanges = ParseScanRanges(ScanSpec.RangesText).ToList();
            var dataType = ScanDataTypeRegistry.Get(ScanSpec.SelectedDataType);
            var summaryPlots = new List<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)>();
            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var read = dataType.Read(file, inputCandidates, outputCandidates);
                    var ranges = ResolveScanRanges(read, configuredRanges, dataType);

                    if (ranges.Count == 0)
                        throw new InvalidOperationException("SCAN range를 계산할 수 없습니다.");

                    var metrics = ranges
                        .Select(r => dataType.ComputeMetrics(read, r))
                        .ToList();

                    var refRange = ranges[0];
                    var (refXs, refYs) = dataType.BuildReferenceLine(read, refRange);
                    string stem = Path.GetFileNameWithoutExtension(file);
                    string plotPath = "";

                    if (ScanSpec.SavePlotImage)
                    {
                        plotPath = Path.Combine(plotDir, $"{SanitizeFileName(stem)}_scan.png");
                        SaveScanPlotPng(
                            read.UpInput,
                            read.UpOutput,
                            read.DownInput,
                            read.DownOutput,
                            refXs,
                            refYs,
                            plotPath,
                            stem,
                            read.InputColumn,
                            read.OutputColumn,
                            metrics,
                            useThousandYTicks: string.Equals(dataType.Name, "AKM", StringComparison.OrdinalIgnoreCase));
                    }

                    var m1 = metrics.ElementAtOrDefault(0);
                    var m2 = metrics.ElementAtOrDefault(1);
                    var row = new ScanResultRow
                    {
                        FileName = Path.GetFileName(file),
                        HeaderRow = read.HeaderRow,
                        InputColumn = read.InputColumn,
                        OutputColumn = read.OutputColumn,
                        UpCount = read.UpOutput.Count,
                        DownCount = read.DownOutput.Count,
                        Range1 = m1 is null ? "" : FormatRange(m1.MinInput, m1.MaxInput),
                        Hys1 = m1 is null || double.IsNaN(m1.Hys) ? null : Math.Round(m1.Hys, 3),
                        Li1 = m1 is null || double.IsNaN(m1.Linearity) ? null : Math.Round(m1.Linearity, 3),
                        Range2 = m2 is null ? "" : FormatRange(m2.MinInput, m2.MaxInput),
                        Hys2 = m2 is null || double.IsNaN(m2.Hys) ? null : Math.Round(m2.Hys, 3),
                        Li2 = m2 is null || double.IsNaN(m2.Linearity) ? null : Math.Round(m2.Linearity, 3),
                        PlotPath = plotPath,
                    };

                    ScanResults.Add(row);
                    PlotFiles.Add(new PlotFileItem(
                        row.FileName,
                        file,
                        read.UpInput,
                        read.UpOutput,
                        read.DownInput,
                        read.DownOutput,
                        refXs,
                        refYs));

                    if (!string.IsNullOrWhiteSpace(plotPath))
                    {
                        summaryPlots.Add((
                            row.FileName,
                            plotPath,
                            row.HeaderRow,
                            row.InputColumn,
                            row.OutputColumn,
                            row.UpCount,
                            row.DownCount));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 75.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();
            ApplyScanJudgementToAll();

            var summaryPath = Path.Combine(outDir, "SCAN_Summary.csv");
            using (var sw = new StreamWriter(summaryPath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("FileName,Judge,Reason,HeaderRow,InputColumn,OutputColumn,UpCount,DownCount,Range1,Hys1,LI1,Range2,Hys2,LI2");
                foreach (var r in ScanResults)
                {
                    sw.WriteLine(string.Join(",",
                        Esc(r.FileName),
                        Esc(r.Judge),
                        Esc(r.JudgeReason),
                        r.HeaderRow.ToString(CultureInfo.InvariantCulture),
                        Esc(r.InputColumn),
                        Esc(r.OutputColumn),
                        r.UpCount.ToString(CultureInfo.InvariantCulture),
                        r.DownCount.ToString(CultureInfo.InvariantCulture),
                        Esc(r.Range1),
                        r.Hys1?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.Li1?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        Esc(r.Range2),
                        r.Hys2?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.Li2?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""));
                }
            }

            var scanDt = BuildScanSummaryTable(ScanResults);
            RawPreview = TakePreview(scanDt, 30).DefaultView;
            RawMergedLabel = $"SCAN 요약 CSV 저장: {summaryPath}";

            string? summaryExcelPath = null;
            if (ScanSpec.SaveSummaryExcel && summaryPlots.Count > 0)
            {
                summaryExcelPath = Path.Combine(outDir, "SCAN_Summary.xlsx");
                SaveScanSummaryExcel(summaryExcelPath, summaryPlots, BuildScanMetadata(files.Length, dataType));
            }

            SelectedScanRow = ScanResults.FirstOrDefault();
            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            var summary = new StringBuilder();
            summary.AppendLine($"Output folder: {outDir}");
            summary.AppendLine("- SCAN_Summary.csv");
            if (ScanSpec.SavePlotImage)
                summary.AppendLine($"- Plot PNGs: {plotDir}");
            if (ScanSpec.SaveSummaryExcel && !string.IsNullOrWhiteSpace(summaryExcelPath))
                summary.AppendLine("- SCAN_Summary.xlsx");
            if (failures.Count > 0)
                summary.AppendLine($"- 실패 {failures.Count}건 (아래 로그 확인)");

            ExportOutputLabel = summary.ToString().TrimEnd();
            ScanSpec.LastSummary = $"Success {ScanResults.Count} / Failed {failures.Count}";
            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "SCAN_ErrorLog.txt");
                SaveScanFailureLog(errorLogPath, failures, files.Length);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowScanFailureMessage(failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            System.Windows.MessageBox.Show(
                ex.Message,
                "SCAN 처리 오류",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExportTiltBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("TILT", files))
            return;

        if (!ConfirmTiltExport(files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "TILT");
            Directory.CreateDirectory(outDir);
            var plotDir = Path.Combine(outDir, "Plots");
            if (TiltSpec.SavePlotImage)
                Directory.CreateDirectory(plotDir);

            var dataType = TiltDataTypeRegistry.Get(TiltSpec.SelectedDataType);
            double? specTilt = ParseSpecNumber(TiltSpec.SpecTilt);
            var summaryPlots = new List<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)>();
            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var read = dataType.Read(file);
                    var metrics = dataType.ComputeMetrics(read);
                    string stem = Path.GetFileNameWithoutExtension(file);
                    string plotPath = "";

                    if (TiltSpec.SavePlotImage)
                    {
                        plotPath = Path.Combine(plotDir, $"{SanitizeFileName(stem)}_tilt.png");
                        SaveTiltPlotPng(
                            read.UpTiltTx,
                            read.UpTiltTy,
                            read.DownTiltTx,
                            read.DownTiltTy,
                            plotPath,
                            stem,
                            specTilt);
                    }

                    var row = new TiltResultRow
                    {
                        FileName = Path.GetFileName(file),
                        UpCount = read.UpTiltTx.Count,
                        DownCount = read.DownTiltTx.Count,
                        PeakTiltUp = Math.Round(metrics.PeakTiltUp, 3),
                        PeakTiltDown = Math.Round(metrics.PeakTiltDown, 3),
                        PeakTilt = Math.Round(metrics.PeakTilt, 3),
                        PlotPath = plotPath,
                    };

                    TiltResults.Add(row);
                    var tiltUpPairs = read.UpTiltTx.Zip(read.UpTiltTy, (tx, ty) => (tx, ty)).ToList();
                    var tiltDownPairs = read.DownTiltTx.Zip(read.DownTiltTy, (tx, ty) => (tx, ty)).ToList();
                    PlotFiles.Add(new PlotFileItem(
                        row.FileName,
                        file,
                        tiltUpPairs,
                        tiltDownPairs));

                    if (!string.IsNullOrWhiteSpace(plotPath))
                    {
                        summaryPlots.Add((
                            row.FileName,
                            plotPath,
                            0,
                            "TiltTx",
                            "TiltTy",
                            row.UpCount,
                            row.DownCount));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 75.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();
            ApplyTiltJudgementToAll();

            var summaryPath = Path.Combine(outDir, "TILT_Summary.csv");
            using (var sw = new StreamWriter(summaryPath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("FileName,Judge,Reason,UpCount,DownCount,PeakTiltUp,PeakTiltDown,PeakTilt");
                foreach (var r in TiltResults)
                {
                    sw.WriteLine(string.Join(",",
                        Esc(r.FileName),
                        Esc(r.Judge),
                        Esc(r.JudgeReason),
                        r.UpCount.ToString(CultureInfo.InvariantCulture),
                        r.DownCount.ToString(CultureInfo.InvariantCulture),
                        r.PeakTiltUp?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.PeakTiltDown?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.PeakTilt?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""));
                }
            }

            var tiltDt = BuildTiltSummaryTable(TiltResults);
            RawPreview = TakePreview(tiltDt, 30).DefaultView;
            RawMergedLabel = $"TILT 요약 CSV 저장: {summaryPath}";

            string? summaryExcelPath = null;
            if (TiltSpec.SaveSummaryExcel && summaryPlots.Count > 0)
            {
                summaryExcelPath = Path.Combine(outDir, "TILT_Summary.xlsx");
                SaveScanSummaryExcel(summaryExcelPath, summaryPlots, BuildTiltMetadata(files.Length, dataType));
            }

            SelectedTiltRow = TiltResults.FirstOrDefault();
            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            var summary = new StringBuilder();
            summary.AppendLine($"Output folder: {outDir}");
            summary.AppendLine("- TILT_Summary.csv");
            if (TiltSpec.SavePlotImage)
                summary.AppendLine($"- Plot PNGs: {plotDir}");
            if (TiltSpec.SaveSummaryExcel && !string.IsNullOrWhiteSpace(summaryExcelPath))
                summary.AppendLine("- TILT_Summary.xlsx");
            if (failures.Count > 0)
                summary.AppendLine($"- 실패 {failures.Count}건 (아래 로그 확인)");

            ExportOutputLabel = summary.ToString().TrimEnd();
            TiltSpec.LastSummary = $"Success {TiltResults.Count} / Failed {failures.Count}";
            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "TILT_ErrorLog.txt");
                SaveTiltFailureLog(errorLogPath, failures, files.Length);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowTiltFailureMessage(failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            System.Windows.MessageBox.Show(
                ex.Message,
                "TILT 처리 오류",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExportSinewaveBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("SINE WAVE", files))
            return;

        if (!ConfirmSinewaveExport(files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "SINE WAVE");
            Directory.CreateDirectory(outDir);
            var plotDir = Path.Combine(outDir, "Plots");
            if (SinewaveSpec.SavePlotImage)
                Directory.CreateDirectory(plotDir);

            var dataType = SinewaveDataTypeRegistry.Get(SinewaveSpec.SelectedDataType);
            double? specMaxErr = ParseSpecNumber(SinewaveSpec.SpecMaxErr);
            var summaryPlots = new List<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)>();
            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var read = dataType.Read(file);
                    var metrics = dataType.ComputeMetrics(read);
                    string stem = Path.GetFileNameWithoutExtension(file);
                    string plotPathX = "";
                    string plotPathY = "";

                    if (SinewaveSpec.SavePlotImage)
                    {
                        plotPathX = Path.Combine(plotDir, $"{SanitizeFileName(stem)}_sinewave_X.png");
                        plotPathY = Path.Combine(plotDir, $"{SanitizeFileName(stem)}_sinewave_Y.png");
                        SaveSinewaveAxisPlotPng(read.TimeMs, read.PosiX, read.HallX, plotPathX, $"{stem} - X-axis");
                        SaveSinewaveAxisPlotPng(read.TimeMs, read.PosiY, read.HallY, plotPathY, $"{stem} - Y-axis");
                    }

                    var row = new SinewaveResultRow
                    {
                        FileName = Path.GetFileName(file),
                        SampleCount = read.TimeMs.Count,
                        MaxErrX = Math.Round(metrics.MaxErrX, 3),
                        MaxErrY = Math.Round(metrics.MaxErrY, 3),
                        PlotPathX = plotPathX,
                        PlotPathY = plotPathY,
                    };

                    SinewaveResults.Add(row);
                    var sinewaveSamples = read.TimeMs
                        .Select((t, si) => (Time: t, PosiX: read.PosiX[si], HallX: read.HallX[si], DiffX: read.DiffX[si],
                            PosiY: read.PosiY[si], HallY: read.HallY[si], DiffY: read.DiffY[si]))
                        .ToList();
                    PlotFiles.Add(new PlotFileItem(
                        row.FileName,
                        file,
                        sinewaveSamples));

                    if (!string.IsNullOrWhiteSpace(plotPathX))
                    {
                        summaryPlots.Add((
                            $"{row.FileName} (X)",
                            plotPathX,
                            read.HeaderRow,
                            "Posi_X",
                            "Hall_X",
                            row.SampleCount,
                            0));
                    }
                    if (!string.IsNullOrWhiteSpace(plotPathY))
                    {
                        summaryPlots.Add((
                            $"{row.FileName} (Y)",
                            plotPathY,
                            read.HeaderRow,
                            "Posi_Y",
                            "Hall_Y",
                            row.SampleCount,
                            0));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 75.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();
            ApplySinewaveJudgementToAll();

            var summaryPath = Path.Combine(outDir, "SINEWAVE_Summary.csv");
            using (var sw = new StreamWriter(summaryPath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("FileName,Judge,Reason,SampleCount,MaxErrX,MaxErrY");
                foreach (var r in SinewaveResults)
                {
                    sw.WriteLine(string.Join(",",
                        Esc(r.FileName),
                        Esc(r.Judge),
                        Esc(r.JudgeReason),
                        r.SampleCount.ToString(CultureInfo.InvariantCulture),
                        r.MaxErrX?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.MaxErrY?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""));
                }
            }

            var sinewaveDt = BuildSinewaveSummaryTable(SinewaveResults);
            RawPreview = TakePreview(sinewaveDt, 30).DefaultView;
            RawMergedLabel = $"SINE WAVE 요약 CSV 저장: {summaryPath}";

            string? summaryExcelPath = null;
            if (SinewaveSpec.SaveSummaryExcel && summaryPlots.Count > 0)
            {
                summaryExcelPath = Path.Combine(outDir, "SINEWAVE_Summary.xlsx");
                SaveScanSummaryExcel(summaryExcelPath, summaryPlots, BuildSinewaveMetadata(files.Length, dataType));
            }

            SelectedSinewaveRow = SinewaveResults.FirstOrDefault();
            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            var summary = new StringBuilder();
            summary.AppendLine($"Output folder: {outDir}");
            summary.AppendLine("- SINEWAVE_Summary.csv");
            if (SinewaveSpec.SavePlotImage)
                summary.AppendLine($"- Plot PNGs: {plotDir}");
            if (SinewaveSpec.SaveSummaryExcel && !string.IsNullOrWhiteSpace(summaryExcelPath))
                summary.AppendLine("- SINEWAVE_Summary.xlsx");
            if (failures.Count > 0)
                summary.AppendLine($"- 실패 {failures.Count}건 (아래 로그 확인)");

            ExportOutputLabel = summary.ToString().TrimEnd();
            SinewaveSpec.LastSummary = $"Success {SinewaveResults.Count} / Failed {failures.Count}";
            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "SINEWAVE_ErrorLog.txt");
                SaveSinewaveFailureLog(errorLogPath, failures, files.Length);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowSinewaveFailureMessage(failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            System.Windows.MessageBox.Show(
                ex.Message,
                "SINE WAVE 처리 오류",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExportCircleBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("CIRCLE TEST", files))
            return;

        if (!ConfirmCircleExport(files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "CIRCLE TEST");
            Directory.CreateDirectory(outDir);
            var plotDir = Path.Combine(outDir, "Plots");
            if (CircleSpec.SavePlotImage)
                Directory.CreateDirectory(plotDir);

            var dataType = CircleTestDataTypeRegistry.Get(CircleSpec.SelectedDataType);
            double? specMaxErr = ParseSpecNumber(CircleSpec.SpecMaxErr);
            var summaryPlots = new List<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)>();
            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var read = dataType.Read(file);
                    var metrics = dataType.ComputeMetrics(read);
                    string stem = Path.GetFileNameWithoutExtension(file);
                    string plotPath = "";

                    if (CircleSpec.SavePlotImage)
                    {
                        plotPath = Path.Combine(plotDir, $"{SanitizeFileName(stem)}_circle.png");
                        SaveCirclePlotPng(read.ReadVtX, read.ReadVtY, read.HallX, read.HallY, plotPath, stem);
                    }

                    var row = new CircleResultRow
                    {
                        FileName = Path.GetFileName(file),
                        SampleCount = read.TimeMs.Count,
                        MaxErrX = Math.Round(metrics.MaxErrX, 3),
                        MaxErrY = Math.Round(metrics.MaxErrY, 3),
                        PlotPathX = plotPath,
                        PlotPathY = "",
                    };

                    CircleResults.Add(row);
                    var circleSamples = read.TimeMs
                        .Select((t, si) => new CircleSample(t, read.ReadVtX[si], read.HallX[si], read.DiffX[si],
                            read.ReadVtY[si], read.HallY[si], read.DiffY[si]))
                        .ToList();
                    PlotFiles.Add(new PlotFileItem(
                        row.FileName,
                        file,
                        circleSamples));

                    if (!string.IsNullOrWhiteSpace(plotPath))
                    {
                        summaryPlots.Add((
                            row.FileName,
                            plotPath,
                            read.HeaderRow,
                            "ReadVt_X/Y",
                            "Hall_X/Y",
                            row.SampleCount,
                            0));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 75.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();
            ApplyCircleJudgementToAll();

            var summaryPath = Path.Combine(outDir, "CIRCLE_Summary.csv");
            using (var sw = new StreamWriter(summaryPath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("FileName,Judge,Reason,SampleCount,MaxErrX,MaxErrY");
                foreach (var r in CircleResults)
                {
                    sw.WriteLine(string.Join(",",
                        Esc(r.FileName),
                        Esc(r.Judge),
                        Esc(r.JudgeReason),
                        r.SampleCount.ToString(CultureInfo.InvariantCulture),
                        r.MaxErrX?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.MaxErrY?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""));
                }
            }

            var circleDt = BuildCircleSummaryTable(CircleResults);
            RawPreview = TakePreview(circleDt, 30).DefaultView;
            RawMergedLabel = $"CIRCLE TEST 요약 CSV 저장: {summaryPath}";

            string? summaryExcelPath = null;
            if (CircleSpec.SaveSummaryExcel && summaryPlots.Count > 0)
            {
                summaryExcelPath = Path.Combine(outDir, "CIRCLE_Summary.xlsx");
                SaveScanSummaryExcel(summaryExcelPath, summaryPlots, BuildCircleMetadata(files.Length, dataType));
            }

            SelectedCircleRow = CircleResults.FirstOrDefault();
            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            var summary = new StringBuilder();
            summary.AppendLine($"Output folder: {outDir}");
            summary.AppendLine("- CIRCLE_Summary.csv");
            if (CircleSpec.SavePlotImage)
                summary.AppendLine($"- Plot PNGs: {plotDir}");
            if (CircleSpec.SaveSummaryExcel && !string.IsNullOrWhiteSpace(summaryExcelPath))
                summary.AppendLine("- CIRCLE_Summary.xlsx");
            if (failures.Count > 0)
                summary.AppendLine($"- 실패 {failures.Count}건 (아래 로그 확인)");

            ExportOutputLabel = summary.ToString().TrimEnd();
            CircleSpec.LastSummary = $"Success {CircleResults.Count} / Failed {failures.Count}";
            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "CIRCLE_ErrorLog.txt");
                SaveCircleFailureLog(errorLogPath, failures, files.Length);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowCircleFailureMessage(failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            System.Windows.MessageBox.Show(
                ex.Message,
                "CIRCLE TEST 처리 오류",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExportSquareBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("SQUARE TEST", files))
            return;

        if (!ConfirmSquareExport(files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "SQUARE TEST");
            Directory.CreateDirectory(outDir);
            var plotDir = Path.Combine(outDir, "Plots");
            if (SquareSpec.SavePlotImage)
                Directory.CreateDirectory(plotDir);

            var dataType = SquareTestDataTypeRegistry.Get(SquareSpec.SelectedDataType);
            double toleranceBandPercent = ParseSpecNumber(SquareSpec.SpecToleranceBandPercent) ?? 5;
            var summaryPlots = new List<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)>();
            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var read = dataType.Read(file);
                    var metrics = dataType.ComputeMetrics(read, toleranceBandPercent);
                    string stem = Path.GetFileNameWithoutExtension(file);
                    int sampleCount = read.Axes.Count > 0 ? read.Axes[0].TimeMs.Count : 0;
                    string firstPlotPath = "";

                    if (SquareSpec.SavePlotImage)
                    {
                        foreach (var axis in read.Axes)
                        {
                            var axisPlotPath = Path.Combine(plotDir, $"{SanitizeFileName(stem)}_square_{axis.Label}.png");
                            SaveSinewaveAxisPlotPng(axis.TimeMs, axis.Target, axis.Value, axisPlotPath, $"{stem} - {axis.Label}");
                            if (string.IsNullOrWhiteSpace(firstPlotPath))
                                firstPlotPath = axisPlotPath;

                            summaryPlots.Add((
                                $"{Path.GetFileName(file)} ({axis.Label})",
                                axisPlotPath,
                                read.HeaderRow,
                                "Target",
                                "Value",
                                axis.TimeMs.Count,
                                0));
                        }
                    }

                    var row = new SquareResultRow
                    {
                        FileName = Path.GetFileName(file),
                        SampleCount = sampleCount,
                        MaxSettlingMs = metrics.MaxSettlingMs is double sc ? Math.Round(sc, 3) : null,
                        MaxOvershoot = metrics.MaxOvershoot is double oc ? Math.Round(oc, 3) : null,
                        MaxSettlingUpMs = metrics.MaxSettlingUpMs is double su ? Math.Round(su, 3) : null,
                        MaxOvershootUp = metrics.MaxOvershootUp is double ou ? Math.Round(ou, 3) : null,
                        MaxSettlingDownMs = metrics.MaxSettlingDownMs is double sd ? Math.Round(sd, 3) : null,
                        MaxOvershootDown = metrics.MaxOvershootDown is double od ? Math.Round(od, 3) : null,
                        PlotPath = firstPlotPath,
                    };

                    SquareResults.Add(row);
                    PlotFiles.Add(new PlotFileItem(row.FileName, file, read.Axes));
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 75.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();
            ApplySquareJudgementToAll();

            var summaryPath = Path.Combine(outDir, "SQUARE_Summary.csv");
            using (var sw = new StreamWriter(summaryPath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("FileName,Judge,Reason,SampleCount,MaxSettlingMs,MaxOvershoot,MaxSettlingUpMs,MaxOvershootUp,MaxSettlingDownMs,MaxOvershootDown");
                foreach (var r in SquareResults)
                {
                    sw.WriteLine(string.Join(",",
                        Esc(r.FileName),
                        Esc(r.Judge),
                        Esc(r.JudgeReason),
                        r.SampleCount.ToString(CultureInfo.InvariantCulture),
                        r.MaxSettlingMs?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.MaxOvershoot?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.MaxSettlingUpMs?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.MaxOvershootUp?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.MaxSettlingDownMs?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                        r.MaxOvershootDown?.ToString("0.###", CultureInfo.InvariantCulture) ?? ""));
                }
            }

            var squareDt = BuildSquareSummaryTable(SquareResults);
            RawPreview = TakePreview(squareDt, 30).DefaultView;
            RawMergedLabel = $"SQUARE TEST 요약 CSV 저장: {summaryPath}";

            string? summaryExcelPath = null;
            if (SquareSpec.SaveSummaryExcel && summaryPlots.Count > 0)
            {
                summaryExcelPath = Path.Combine(outDir, "SQUARE_Summary.xlsx");
                SaveScanSummaryExcel(summaryExcelPath, summaryPlots, BuildSquareMetadata(files.Length, dataType));
            }

            SelectedSquareRow = SquareResults.FirstOrDefault();
            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            var summary = new StringBuilder();
            summary.AppendLine($"Output folder: {outDir}");
            summary.AppendLine("- SQUARE_Summary.csv");
            if (SquareSpec.SavePlotImage)
                summary.AppendLine($"- Plot PNGs: {plotDir}");
            if (SquareSpec.SaveSummaryExcel && !string.IsNullOrWhiteSpace(summaryExcelPath))
                summary.AppendLine("- SQUARE_Summary.xlsx");
            if (failures.Count > 0)
                summary.AppendLine($"- 실패 {failures.Count}건 (아래 로그 확인)");

            ExportOutputLabel = summary.ToString().TrimEnd();
            SquareSpec.LastSummary = $"Success {SquareResults.Count} / Failed {failures.Count}";
            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "SQUARE_ErrorLog.txt");
                SaveSquareFailureLog(errorLogPath, failures, files.Length);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowSquareFailureMessage(failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            System.Windows.MessageBox.Show(
                ex.Message,
                "SQUARE TEST 처리 오류",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    // 그룹(동일 스텝 조건)별로 계산된 지표 + 원본 시계열. 표/CSV/PNG/Excel 생성에 공용으로 쓰인다.
    private sealed record SettlingGroupPlotData(
        string GroupLabel,
        IReadOnlyList<SettlingSeries> Series,
        SettlingResult Result,
        double ToleranceBand,
        double Displacement);

    private void ExportSettlingBatch()
    {
        if (SelectedSettlingVendorOption is null)
        {
            ExportOutputLabel = "Data Type(DW/Robocube/K-star)를 선택하세요.";
            return;
        }

        var files = Directory.GetFiles(ImportedFolder, SelectedSettlingVendorOption.Handler.FileSearchPattern, SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        if (!EnsureFilesToProcess("SETTLING TIME", files))
            return;

        if (!ConfirmExportDialog("SETTLING TIME", "SETTLING TIME", SelectedSettlingVendorOption.Name, files))
            return;

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "SETTLING TIME");
            Directory.CreateDirectory(outDir);

            var dataType = SelectedSettlingVendorOption.Handler;
            double toleranceBand = ParseSpecNumber(SettlingSpec.ToleranceBand) ?? 0;
            double tailFraction = (ParseSpecNumber(SettlingSpec.TailWindowPercent) ?? 20) / 100.0;

            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var seriesList = dataType.Read(file);
                    string fileName = Path.GetFileName(file);
                    var grouped = GroupSettlingSeries(seriesList);
                    var groupMetrics = BuildSettlingGroupMetrics(grouped, toleranceBand, tailFraction, dataType);
                    var rows = BuildSettlingRowsForFile(fileName, groupMetrics);

                    foreach (var r in rows)
                        SettlingResults.Add(r);

                    var plotGroups = grouped
                        .Select(g => (g.GroupLabel, (IReadOnlyList<SettlingSeries>)g.Series))
                        .ToList();
                    PlotFiles.Add(new PlotFileItem(fileName, file, plotGroups));
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                ProgressValue = (i + 1) * 75.0 / Math.Max(1, files.Length);
            }

            RefreshPlotFilter();

            var summaryPath = Path.Combine(outDir, "Settling_Summary.csv");
            var settlingDt = BuildSettlingSummaryTable(SettlingResults);
            SaveDataTableToCsv(settlingDt, summaryPath);
            RawPreview = TakePreview(settlingDt, 30).DefaultView;
            RawMergedLabel = $"SETTLING TIME 요약 CSV 저장: {summaryPath}";

            SelectedSettlingRow = SettlingResults.FirstOrDefault();
            IsEmpty = false;
            IsPlotTab = true;
            ProgressValue = 100;

            var summary = new StringBuilder();
            summary.AppendLine($"Output folder: {outDir}");
            summary.AppendLine("- Settling_Summary.csv");
            if (failures.Count > 0)
                summary.AppendLine($"- 실패 {failures.Count}건 (아래 로그 확인)");

            ExportOutputLabel = summary.ToString().TrimEnd();
            SettlingSpec.LastSummary = $"Success {SettlingResults.Count} rows / Failed {failures.Count} files";
            if (failures.Count > 0)
            {
                var errorLogPath = Path.Combine(outDir, "SETTLING_ErrorLog.txt");
                SaveExportFailureLog(errorLogPath, "SETTLING TIME", SelectedSettlingVendorOption.Name,
                    failures, files.Length, SettlingResults.Count);
                RawMergedLabel += Environment.NewLine + $"Error log saved: {errorLogPath}";
                ShowExportFailureMessage("SETTLING TIME", failures);
            }
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
            ShowExportExceptionMessage("SETTLING TIME", ex);
        }
        finally
        {
            IsBusy = false;
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    // 동일 스텝 조건(그룹)끼리 묶는다. DW는 "X"/"Y" 라벨에 " #N" 접미사가 없어 각각 단독 그룹이 되고,
    // Robocube는 "{조건} #1".."#5" 접미사를 떼어낸 값으로 5회 반복이 한 그룹으로 묶인다.
    private static List<(string GroupLabel, List<SettlingSeries> Series)> GroupSettlingSeries(IReadOnlyList<SettlingSeries> seriesList)
        => seriesList
            .Select(s => (Series: s, GroupKey: StripCountSuffix(s.Label)))
            .GroupBy(x => x.GroupKey)
            .Select(g => (GroupLabel: g.Key, Series: g.Select(x => x.Series).ToList()))
            .ToList();

    // 조건(GroupLabel)별 Tolerance Band를 찾는다. 처음 보는 조건이면 현재 기본값으로 새 행을 추가해서
    // 사용자가 이후에 조건별로 값을 고칠 수 있게 한다.
    private double ResolveToleranceBand(string groupLabel, double defaultToleranceBand)
    {
        var row = SettlingSpec.ConditionTolerances.FirstOrDefault(c => c.GroupLabel == groupLabel);
        if (row is null)
        {
            SettlingSpec.ConditionTolerances.Add(new SettlingConditionTolerance(
                groupLabel, defaultToleranceBand.ToString(CultureInfo.InvariantCulture)));
            return defaultToleranceBand;
        }

        return ParseSpecNumber(row.ToleranceBand) ?? defaultToleranceBand;
    }

    private List<SettlingGroupPlotData> BuildSettlingGroupMetrics(
        List<(string GroupLabel, List<SettlingSeries> Series)> grouped,
        double defaultToleranceBand,
        double tailFraction,
        ISettlingDataTypeHandler dataType)
    {
        double judgeStartTimeMs = dataType.JudgeStartTimeMs;
        var result = new List<SettlingGroupPlotData>();

        foreach (var (label, series) in grouped)
        {
            double toleranceBand = ResolveToleranceBand(label, defaultToleranceBand);
            var perSeries = series
                .Select(s => SettlingMetrics.Compute(s.TimeMs, s.Value, s.ExplicitTarget, toleranceBand, tailFraction, judgeStartTimeMs))
                .ToList();
            var agg = SettlingMetrics.AggregateExcludingMinMax(perSeries);
            double displacement = series.Count > 0 ? series.Average(s => SettlingMetrics.ComputeDisplacement(s.Value, tailFraction)) : 0;

            result.Add(new SettlingGroupPlotData(label, series, agg, toleranceBand, displacement));
        }

        return result;
    }

    private List<SettlingResultRow> BuildSettlingRowsForFile(string fileName, List<SettlingGroupPlotData> groups)
    {
        var rows = groups.Select(g => new SettlingResultRow
        {
            FileName = fileName,
            GroupLabel = g.GroupLabel,
            Target = g.Result.Target,
            SettlingTimeMs = g.Result.SettlingTimeMs,
            Overshoot = g.Result.Overshoot,
            Displacement = g.Displacement,
        }).ToList();

        foreach (var row in rows)
        {
            var (judge, reason) = JudgeSettlingBySpec(row);
            row.Judge = judge;
            row.JudgeReason = reason;
        }

        return rows;
    }

    private static string StripCountSuffix(string label)
    {
        int idx = label.LastIndexOf(" #", StringComparison.Ordinal);
        return idx >= 0 ? label[..idx] : label;
    }

    private (string judge, string reason) JudgeSettlingBySpec(SettlingResultRow row)
    {
        double? maxSpec = ParseSpecNumber(SettlingSpec.MaxSettlingTimeMs);
        if (maxSpec is double max)
        {
            if (row.SettlingTimeMs is double st)
            {
                if (st > max)
                    return ("NG", $"SettlingTime>{max:0.##}ms");
            }
            else
            {
                return ("NG", "SettlingTime=N/A");
            }
        }

        return ("PASS", "");
    }

    private void ApplySettlingJudgementToAll()
    {
        for (int i = 0; i < SettlingResults.Count; i++)
        {
            var row = SettlingResults[i];
            var (judge, reason) = JudgeSettlingBySpec(row);
            if (row.Judge == judge && row.JudgeReason == reason)
                continue;

            SettlingResults[i] = new SettlingResultRow
            {
                FileName = row.FileName,
                GroupLabel = row.GroupLabel,
                Target = row.Target,
                SettlingTimeMs = row.SettlingTimeMs,
                Overshoot = row.Overshoot,
                Displacement = row.Displacement,
                Judge = judge,
                JudgeReason = reason,
                PlotPath = row.PlotPath,
            };
        }

        if (SelectedSettlingRow is not null)
        {
            var selected = SettlingResults.FirstOrDefault(x =>
                x.FileName == SelectedSettlingRow.FileName && x.GroupLabel == SelectedSettlingRow.GroupLabel);
            if (selected is not null)
                SelectedSettlingRow = selected;
        }
    }

    private void RecalculateSettlingResultsFromCurrentPlotData()
    {
        if (PlotFiles.Count == 0)
            return;

        double toleranceBand = ParseSpecNumber(SettlingSpec.ToleranceBand) ?? 0;
        double tailFraction = (ParseSpecNumber(SettlingSpec.TailWindowPercent) ?? 20) / 100.0;

        var dataType = SelectedSettlingVendorOption?.Handler ?? SettlingDataTypeRegistry.Get(null);
        var selectedFile = SelectedSettlingRow?.FileName;
        SettlingResults.Clear();

        foreach (var item in PlotFiles.Where(p => p.IsSettling))
        {
            var grouped = item.SettlingGroups.Select(g => (g.GroupLabel, Series: g.Series.ToList())).ToList();
            var groupMetrics = BuildSettlingGroupMetrics(grouped, toleranceBand, tailFraction, dataType);
            var rows = BuildSettlingRowsForFile(item.FileName, groupMetrics);
            foreach (var r in rows)
                SettlingResults.Add(r);
        }

        if (!string.IsNullOrWhiteSpace(selectedFile))
            SelectedSettlingRow = SettlingResults.FirstOrDefault(x => x.FileName == selectedFile) ?? SettlingResults.FirstOrDefault();
        else if (SettlingResults.Count > 0)
            SelectedSettlingRow = SettlingResults[0];

        UpdateRecentFromSettlingRow(SelectedSettlingRow);
    }

    private void UpdateRecentFromSettlingRow(SettlingResultRow? row)
    {
        if (row is null) return;

        SettlingSpec.LastTarget = row.Target?.ToString("0.00") ?? "N/A";
        SettlingSpec.LastSettlingTimeMs = row.SettlingTimeMs?.ToString("0.00") ?? "N/A";
        SettlingSpec.LastOvershoot = row.Overshoot?.ToString("0.00") ?? "N/A";
        SettlingSpec.LastJudge = string.IsNullOrWhiteSpace(row.Judge) ? "-" : row.Judge;
        SettlingSpec.LastReason = string.IsNullOrWhiteSpace(row.JudgeReason) ? "-" : row.JudgeReason;
        SettlingSpec.SourceFile = $"{row.FileName} ({row.GroupLabel})";
    }

    private static bool IsSettlingJudgementSpecProperty(string? propertyName)
        => propertyName is nameof(SettlingSpecViewModel.MaxSettlingTimeMs);

    private static bool IsSettlingRecalculationSpecProperty(string? propertyName)
        => propertyName is nameof(SettlingSpecViewModel.ToleranceBand) or nameof(SettlingSpecViewModel.TailWindowPercent);

    private static DataTable BuildSettlingSummaryTable(IEnumerable<SettlingResultRow> rows)
    {
        // 파일당 1행, 조건(GroupLabel)마다 Settling Time 값 하나만 옆으로 펼친다.
        var rowList = rows.ToList();
        var conditionLabels = rowList.Select(r => r.GroupLabel).Distinct().ToList();

        var dt = new DataTable();
        dt.Columns.Add("FileName", typeof(string));
        foreach (var label in conditionLabels)
            dt.Columns.Add($"{label} Settling(ms)", typeof(double));

        foreach (var fileGroup in rowList.GroupBy(r => r.FileName))
        {
            var byLabel = fileGroup.ToDictionary(r => r.GroupLabel);
            var row = dt.NewRow();
            row["FileName"] = fileGroup.Key;
            foreach (var label in conditionLabels)
            {
                if (!byLabel.TryGetValue(label, out var r))
                    continue;

                row[$"{label} Settling(ms)"] = r.SettlingTimeMs is null ? DBNull.Value : r.SettlingTimeMs.Value;
            }
            dt.Rows.Add(row);
        }

        return dt;
    }

    // ── Export 공통 안내 ────────────────────────────────────────────────
    // FRA / DC SENSITIVITY / SETTLING TIME 은 원래 확인창·실패창이 없어서 다른 항목과 동작이 달랐다.
    // 기존 5개 항목(SCAN/TILT/SINE/CIRCLE/SQUARE)의 전용 메서드는 그대로 두고,
    // 새로 붙이는 3개 항목은 아래 공용 헬퍼를 쓴다.

    // 처리할 파일이 0건이면 안내하고 중단한다.
    private bool EnsureFilesToProcess(string inspectionName, IReadOnlyList<string> files)
    {
        if (files.Count > 0)
            return true;

        ExportOutputLabel = $"{inspectionName}: 처리할 파일이 없습니다.";
        System.Windows.MessageBox.Show(
            $"선택한 폴더에 처리할 파일이 없습니다.{Environment.NewLine}{Environment.NewLine}" +
            $"폴더: {ImportedFolder}",
            $"{inspectionName} Export",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
        return false;
    }

    private bool ConfirmExportDialog(string inspectionName, string outputFolderName, string dataTypeText, IReadOnlyList<string> files)
    {
        string outputPath = HasImportedFolder
            ? Path.Combine(ImportedFolder, "OUTPUT", outputFolderName)
            : "";

        string message =
            $"{inspectionName} Export를 시작할까요?{Environment.NewLine}{Environment.NewLine}" +
            $"Data Type: {dataTypeText}{Environment.NewLine}" +
            $"Target files: {files.Count}{Environment.NewLine}" +
            $"Output: {outputPath}";

        return System.Windows.MessageBox.Show(
            message,
            $"{inspectionName} Export 확인",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.OK;
    }

    private void SaveExportFailureLog(
        string path, string inspectionName, string dataTypeText,
        IReadOnlyList<string> failures, int targetFileCount, int successCount)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine($"{inspectionName} Error Log");
        sw.WriteLine($"App Version: {GetAppVersion()}");
        sw.WriteLine($"Data Type: {dataTypeText}");
        sw.WriteLine($"Target Files: {targetFileCount}");
        sw.WriteLine($"Success: {successCount}");
        sw.WriteLine($"Failed: {failures.Count}");
        sw.WriteLine();
        foreach (var failure in failures)
            sw.WriteLine(failure);
    }

    private static void ShowExportFailureMessage(string inspectionName, IReadOnlyList<string> failures)
    {
        var preview = string.Join(Environment.NewLine, failures.Take(20));
        var tail = failures.Count > 20 ? $"{Environment.NewLine}..." : "";
        System.Windows.MessageBox.Show(
            preview + tail,
            $"{inspectionName} 파일 처리 오류",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private static void ShowExportExceptionMessage(string inspectionName, Exception ex)
        => System.Windows.MessageBox.Show(
            ex.Message,
            $"{inspectionName} 처리 오류",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);

    private bool ConfirmScanExport(IReadOnlyList<string> files)
    {
        string outputPath = HasImportedFolder
            ? Path.Combine(ImportedFolder, "OUTPUT", "SCAN")
            : "";

        string message =
            $"SCAN Export를 시작할까요?{Environment.NewLine}{Environment.NewLine}" +
            $"Data Type: {ScanSpec.SelectedDataType}{Environment.NewLine}" +
            $"Target files: {files.Count}{Environment.NewLine}" +
            $"Output: {outputPath}";

        return System.Windows.MessageBox.Show(
            message,
            "SCAN Export 확인",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.OK;
    }

    private bool ConfirmTiltExport(IReadOnlyList<string> files)
    {
        string outputPath = HasImportedFolder
            ? Path.Combine(ImportedFolder, "OUTPUT", "TILT")
            : "";

        string message =
            $"TILT Export를 시작할까요?{Environment.NewLine}{Environment.NewLine}" +
            $"Data Type: {TiltSpec.SelectedDataType}{Environment.NewLine}" +
            $"Target files: {files.Count}{Environment.NewLine}" +
            $"Output: {outputPath}";

        return System.Windows.MessageBox.Show(
            message,
            "TILT Export 확인",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.OK;
    }

    private bool ConfirmSinewaveExport(IReadOnlyList<string> files)
    {
        string outputPath = HasImportedFolder
            ? Path.Combine(ImportedFolder, "OUTPUT", "SINE WAVE")
            : "";

        string message =
            $"SINE WAVE Export를 시작할까요?{Environment.NewLine}{Environment.NewLine}" +
            $"Data Type: {SinewaveSpec.SelectedDataType}{Environment.NewLine}" +
            $"Target files: {files.Count}{Environment.NewLine}" +
            $"Output: {outputPath}";

        return System.Windows.MessageBox.Show(
            message,
            "SINE WAVE Export 확인",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.OK;
    }

    private bool ConfirmCircleExport(IReadOnlyList<string> files)
    {
        string outputPath = HasImportedFolder
            ? Path.Combine(ImportedFolder, "OUTPUT", "CIRCLE TEST")
            : "";

        string message =
            $"CIRCLE TEST Export를 시작할까요?{Environment.NewLine}{Environment.NewLine}" +
            $"Data Type: {CircleSpec.SelectedDataType}{Environment.NewLine}" +
            $"Target files: {files.Count}{Environment.NewLine}" +
            $"Output: {outputPath}";

        return System.Windows.MessageBox.Show(
            message,
            "CIRCLE TEST Export 확인",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.OK;
    }

    private bool ConfirmSquareExport(IReadOnlyList<string> files)
    {
        string outputPath = HasImportedFolder
            ? Path.Combine(ImportedFolder, "OUTPUT", "SQUARE TEST")
            : "";

        string message =
            $"SQUARE TEST Export를 시작할까요?{Environment.NewLine}{Environment.NewLine}" +
            $"Data Type: {SquareSpec.SelectedDataType}{Environment.NewLine}" +
            $"Target files: {files.Count}{Environment.NewLine}" +
            $"Output: {outputPath}";

        return System.Windows.MessageBox.Show(
            message,
            "SQUARE TEST Export 확인",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.OK;
    }

    private List<ScanRangeDefinition> ResolveScanRanges(
        ScanReadResult read,
        IReadOnlyList<ScanRangeDefinition> configuredRanges,
        IScanDataTypeHandler dataType)
    {
        if (dataType.UsesAutomaticRange && read.UpInput.Count > 1)
            return new List<ScanRangeDefinition> { new(read.UpInput.Min(), read.UpInput.Max()) };

        if (configuredRanges.Count > 0)
            return configuredRanges.ToList();

        var hinted = read.RangeHints.Select(x => new ScanRangeDefinition(x.MinInput, x.MaxInput)).ToList();
        if (hinted.Count > 0)
            return hinted;

        if (read.UpInput.Count > 1)
            return new List<ScanRangeDefinition> { new(read.UpInput.Min(), read.UpInput.Max()) };

        return new List<ScanRangeDefinition>();
    }

    private IReadOnlyList<(string Name, string Value)> BuildScanMetadata(
        int targetFileCount,
        IScanDataTypeHandler dataType)
    {
        return new List<(string Name, string Value)>
        {
            ("App Version", GetAppVersion()),
            ("Inspection", "SCAN"),
            ("Data Type", dataType.Name),
            ("Input Header", dataType.InputHeaderDisplay),
            ("Output Header", dataType.OutputHeaderDisplay),
            ("Target Files", targetFileCount.ToString(CultureInfo.InvariantCulture)),
            ("Range Rule", dataType.UsesAutomaticRange ? dataType.RangeRuleDescription : $"Configured: {ScanSpec.RangesText}"),
            ("HYS Rule", dataType.HysRuleDescription),
            ("LI Rule", dataType.LiRuleDescription),
            ("Created At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        };
    }

    private static string GetAppVersion()
        => typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "unknown";

    private void SaveScanFailureLog(string path, IReadOnlyList<string> failures, int targetFileCount)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("SCAN Error Log");
        sw.WriteLine($"App Version: {GetAppVersion()}");
        sw.WriteLine($"Data Type: {ScanSpec.SelectedDataType}");
        sw.WriteLine($"Target Files: {targetFileCount}");
        sw.WriteLine($"Success: {ScanResults.Count}");
        sw.WriteLine($"Failed: {failures.Count}");
        sw.WriteLine();
        foreach (var failure in failures)
            sw.WriteLine(failure);
    }

    private static void ShowScanFailureMessage(IReadOnlyList<string> failures)
    {
        var preview = string.Join(Environment.NewLine, failures.Take(20));
        var tail = failures.Count > 20 ? $"{Environment.NewLine}..." : "";
        System.Windows.MessageBox.Show(
            preview + tail,
            "SCAN 파일 처리 오류",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private IReadOnlyList<(string Name, string Value)> BuildTiltMetadata(
        int targetFileCount,
        ITiltDataTypeHandler dataType)
    {
        return new List<(string Name, string Value)>
        {
            ("App Version", GetAppVersion()),
            ("Inspection", "TILT"),
            ("Data Type", dataType.Name),
            ("Input Header", dataType.InputHeaderDisplay),
            ("Output Header", dataType.OutputHeaderDisplay),
            ("Target Files", targetFileCount.ToString(CultureInfo.InvariantCulture)),
            ("Metric Rule", dataType.MetricRuleDescription),
            ("Spec Tilt (abs max)", TiltSpec.SpecTilt),
            ("Created At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        };
    }

    private void SaveTiltFailureLog(string path, IReadOnlyList<string> failures, int targetFileCount)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("TILT Error Log");
        sw.WriteLine($"App Version: {GetAppVersion()}");
        sw.WriteLine($"Data Type: {TiltSpec.SelectedDataType}");
        sw.WriteLine($"Target Files: {targetFileCount}");
        sw.WriteLine($"Success: {TiltResults.Count}");
        sw.WriteLine($"Failed: {failures.Count}");
        sw.WriteLine();
        foreach (var failure in failures)
            sw.WriteLine(failure);
    }

    private static void ShowTiltFailureMessage(IReadOnlyList<string> failures)
    {
        var preview = string.Join(Environment.NewLine, failures.Take(20));
        var tail = failures.Count > 20 ? $"{Environment.NewLine}..." : "";
        System.Windows.MessageBox.Show(
            preview + tail,
            "TILT 파일 처리 오류",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private IReadOnlyList<(string Name, string Value)> BuildSinewaveMetadata(
        int targetFileCount,
        ISinewaveDataTypeHandler dataType)
    {
        return new List<(string Name, string Value)>
        {
            ("App Version", GetAppVersion()),
            ("Inspection", "SINE WAVE"),
            ("Data Type", dataType.Name),
            ("Input Header", dataType.InputHeaderDisplay),
            ("Output Header", dataType.OutputHeaderDisplay),
            ("Target Files", targetFileCount.ToString(CultureInfo.InvariantCulture)),
            ("Metric Rule", dataType.MetricRuleDescription),
            ("Spec MaxErr (abs max)", SinewaveSpec.SpecMaxErr),
            ("Created At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        };
    }

    private void SaveSinewaveFailureLog(string path, IReadOnlyList<string> failures, int targetFileCount)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("SINE WAVE Error Log");
        sw.WriteLine($"App Version: {GetAppVersion()}");
        sw.WriteLine($"Data Type: {SinewaveSpec.SelectedDataType}");
        sw.WriteLine($"Target Files: {targetFileCount}");
        sw.WriteLine($"Success: {SinewaveResults.Count}");
        sw.WriteLine($"Failed: {failures.Count}");
        sw.WriteLine();
        foreach (var failure in failures)
            sw.WriteLine(failure);
    }

    private static void ShowSinewaveFailureMessage(IReadOnlyList<string> failures)
    {
        var preview = string.Join(Environment.NewLine, failures.Take(20));
        var tail = failures.Count > 20 ? $"{Environment.NewLine}..." : "";
        System.Windows.MessageBox.Show(
            preview + tail,
            "SINE WAVE 파일 처리 오류",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private IReadOnlyList<(string Name, string Value)> BuildCircleMetadata(
        int targetFileCount,
        ICircleTestDataTypeHandler dataType)
    {
        return new List<(string Name, string Value)>
        {
            ("App Version", GetAppVersion()),
            ("Inspection", "CIRCLE TEST"),
            ("Data Type", dataType.Name),
            ("Input Header", dataType.InputHeaderDisplay),
            ("Output Header", dataType.OutputHeaderDisplay),
            ("Target Files", targetFileCount.ToString(CultureInfo.InvariantCulture)),
            ("Metric Rule", dataType.MetricRuleDescription),
            ("Spec MaxErr (abs max)", CircleSpec.SpecMaxErr),
            ("Created At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        };
    }

    private void SaveCircleFailureLog(string path, IReadOnlyList<string> failures, int targetFileCount)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("CIRCLE TEST Error Log");
        sw.WriteLine($"App Version: {GetAppVersion()}");
        sw.WriteLine($"Data Type: {CircleSpec.SelectedDataType}");
        sw.WriteLine($"Target Files: {targetFileCount}");
        sw.WriteLine($"Success: {CircleResults.Count}");
        sw.WriteLine($"Failed: {failures.Count}");
        sw.WriteLine();
        foreach (var failure in failures)
            sw.WriteLine(failure);
    }

    private static void ShowCircleFailureMessage(IReadOnlyList<string> failures)
    {
        var preview = string.Join(Environment.NewLine, failures.Take(20));
        var tail = failures.Count > 20 ? $"{Environment.NewLine}..." : "";
        System.Windows.MessageBox.Show(
            preview + tail,
            "CIRCLE TEST 파일 처리 오류",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private IReadOnlyList<(string Name, string Value)> BuildSquareMetadata(
        int targetFileCount,
        ISquareTestDataTypeHandler dataType)
    {
        return new List<(string Name, string Value)>
        {
            ("App Version", GetAppVersion()),
            ("Inspection", "SQUARE TEST"),
            ("Data Type", dataType.Name),
            ("Input Header", dataType.InputHeaderDisplay),
            ("Output Header", dataType.OutputHeaderDisplay),
            ("Target Files", targetFileCount.ToString(CultureInfo.InvariantCulture)),
            ("Metric Rule", dataType.MetricRuleDescription),
            ("Tolerance Band (% of Displacement)", SquareSpec.SpecToleranceBandPercent),
            ("Spec Max Settling Time (ms)", SquareSpec.SpecMaxSettlingTimeMs),
            ("Spec Max Overshoot", SquareSpec.SpecMaxOvershoot),
            ("Created At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        };
    }

    private void SaveSquareFailureLog(string path, IReadOnlyList<string> failures, int targetFileCount)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("SQUARE TEST Error Log");
        sw.WriteLine($"App Version: {GetAppVersion()}");
        sw.WriteLine($"Data Type: {SquareSpec.SelectedDataType}");
        sw.WriteLine($"Target Files: {targetFileCount}");
        sw.WriteLine($"Success: {SquareResults.Count}");
        sw.WriteLine($"Failed: {failures.Count}");
        sw.WriteLine();
        foreach (var failure in failures)
            sw.WriteLine(failure);
    }

    private static void ShowSquareFailureMessage(IReadOnlyList<string> failures)
    {
        var preview = string.Join(Environment.NewLine, failures.Take(20));
        var tail = failures.Count > 20 ? $"{Environment.NewLine}..." : "";
        System.Windows.MessageBox.Show(
            preview + tail,
            "SQUARE TEST 파일 처리 오류",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private static void SaveDcSummaryExcel(
        string path,
        IReadOnlyList<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)> items)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Summary");

        const int startRow = 1;
        const int startCol = 1;
        const int columnsPerRow = 5;
        const int imageWidth = 520;
        const int imageHeight = 240;
        const int rowBlockHeight = 255;
        const double columnWidth = 78;

        for (int c = 0; c < columnsPerRow; c++)
            ws.Column(startCol + c).Width = columnWidth;

        for (int i = 0; i < items.Count; i++)
        {
            int gridRow = i / columnsPerRow;
            int gridCol = i % columnsPerRow;

            int anchorRow = startRow + gridRow;
            int anchorCol = startCol + gridCol;

            ws.Row(anchorRow).Height = rowBlockHeight;

            if (File.Exists(items[i].PlotPath))
            {
                ws.AddPicture(items[i].PlotPath)
                    .MoveTo(ws.Cell(anchorRow, anchorCol), 5, 5)
                    .WithSize(imageWidth, imageHeight);
            }
            else
            {
                ws.Cell(anchorRow, anchorCol).Value = items[i].FileName;
            }
        }

        ws.SheetView.FreezeRows(0);
        wb.SaveAs(path);
    }

    private static void SaveScanSummaryExcel(
        string path,
        IReadOnlyList<(string FileName, string PlotPath, int HeaderRow, string InputColumn, string OutputColumn, int UpCount, int DownCount)> items,
        IReadOnlyList<(string Name, string Value)> metadata)
    {
        using var wb = new XLWorkbook();

        var meta = wb.Worksheets.Add("Criteria");
        meta.Cell(1, 1).Value = "Name";
        meta.Cell(1, 2).Value = "Value";
        meta.Range(1, 1, 1, 2).Style.Font.Bold = true;
        for (int i = 0; i < metadata.Count; i++)
        {
            meta.Cell(i + 2, 1).Value = metadata[i].Name;
            meta.Cell(i + 2, 2).Value = metadata[i].Value;
        }
        meta.Columns().AdjustToContents();

        var ws = wb.Worksheets.Add("Plots");
        const int startRow = 1;
        const int startCol = 1;
        const int columnsPerRow = 5;
        const int imageWidth = 520;
        const int imageHeight = 240;
        const int rowBlockHeight = 255;
        const double columnWidth = 78;

        for (int c = 0; c < columnsPerRow; c++)
            ws.Column(startCol + c).Width = columnWidth;

        for (int i = 0; i < items.Count; i++)
        {
            int gridRow = i / columnsPerRow;
            int gridCol = i % columnsPerRow;
            int anchorRow = startRow + gridRow;
            int anchorCol = startCol + gridCol;

            ws.Row(anchorRow).Height = rowBlockHeight;
            if (File.Exists(items[i].PlotPath))
            {
                ws.AddPicture(items[i].PlotPath)
                    .MoveTo(ws.Cell(anchorRow, anchorCol), 5, 5)
                    .WithSize(imageWidth, imageHeight);
            }
            else
            {
                ws.Cell(anchorRow, anchorCol).Value = items[i].FileName;
            }
        }

        wb.SaveAs(path);
    }


    private void PlotItemOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlotFileItem.IsOverlaySelected))
        {
            RefreshSelectedPlotCommands();
        }
    }

    private void SaveSelectedFraOverlay()
    {
        if (!IsFraInspection)
            return;

        var selected = PlotFiles
            .Where(x => x.IsOverlaySelected && x.Points.Count > 0)
            .Select(x => (name: Path.GetFileNameWithoutExtension(x.FileName), pts: (IReadOnlyList<BodePoint>)ApplyFraPhaseMode(x.Points)))
            .ToList();

        if (selected.Count == 0)
        {
            ExportOutputLabel = "선택된 FRA 파일이 없습니다.";
            return;
        }

        string initialDir = HasImportedFolder
            ? Path.Combine(ImportedFolder, "OUTPUT", "FRA")
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(initialDir);

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "선택 그래프 추출",
            Filter = "PNG Image (*.png)|*.png",
            FileName = $"FRA_Selected_Overlay_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            InitialDirectory = initialDir
        };

        if (dlg.ShowDialog() != true)
            return;

        PlotImageExporter.SaveBodeOverlayPng(
            selected,
            dlg.FileName,
            width: 1600,
            height: 1000,
            overlayLabel: SelectedOverlayExportLabel);
        ExportOutputLabel = $"선택 그래프 추출 완료: {dlg.FileName}";
    }

    // 파일 목록에서 고른 파일 하나의 조건(그룹)마다 독립된 PNG를 저장한다. Export 시 자동 저장하지 않고
    // 사용자가 필요한 파일만 골라 버튼으로 직접 추출하도록 한다.
    // 검사항목별 OUTPUT 하위 폴더명. 기존 Export 배치가 쓰는 이름과 동일하게 맞춘다.
    private string CurrentOutputFolderName() => SelectedInspection switch
    {
        "FRA" => "FRA",
        "DC SENSITIVITY" => "DC sensitivity",
        "SCAN" => "SCAN",
        "SETTLING TIME" => "SETTLING TIME",
        "TILT" => "TILT",
        "SINE WAVE" => "SINE WAVE",
        "CIRCLE TEST" => "CIRCLE TEST",
        "SQUARE TEST" => "SQUARE TEST",
        _ => "PLOTS",
    };

    private string EnsureSelectedPlotDir()
    {
        string root = HasImportedFolder
            ? ImportedFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string dir = Path.Combine(root, "OUTPUT", CurrentOutputFolderName(), "Plots");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // 체크한 파일들을 파일당 개별 PNG로 저장한다(검사항목 공통).
    // SETTLING/SQUARE처럼 파일 하나에 조건/축이 여러 개면 그만큼 여러 장이 나온다.
    private void SaveSelectedPlotImages()
    {
        var selected = PlotFiles.Where(x => x.IsOverlaySelected && !x.IsAllOverlayItem).ToList();
        if (selected.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "파일 목록에서 이미지를 저장할 파일을 체크하세요.",
                "선택 파일 이미지 저장",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        string plotDir = EnsureSelectedPlotDir();
        int saved = 0;
        var failures = new List<string>();

        foreach (var item in selected)
        {
            try
            {
                saved += SavePlotImagesForItem(item, plotDir);
            }
            catch (Exception ex)
            {
                failures.Add($"{item.FileName}: {ex.Message}");
            }
        }

        var msg = new StringBuilder();
        msg.AppendLine($"{saved}장 저장 완료 (파일 {selected.Count}개)");
        msg.AppendLine(plotDir);
        if (failures.Count > 0)
        {
            msg.AppendLine();
            msg.AppendLine($"실패 {failures.Count}건:");
            foreach (var f in failures.Take(10))
                msg.AppendLine("  - " + f);
        }

        ExportOutputLabel = $"선택 파일 이미지 저장: {saved}장 / {plotDir}";
        System.Windows.MessageBox.Show(
            msg.ToString().TrimEnd(),
            "선택 파일 이미지 저장",
            System.Windows.MessageBoxButton.OK,
            failures.Count > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
    }

    // 항목 하나를 PNG로 저장하고, 저장한 장수를 돌려준다.
    private int SavePlotImagesForItem(PlotFileItem item, string plotDir)
    {
        string stem = SanitizeFileName(Path.GetFileNameWithoutExtension(item.FileName));

        if (item.IsSettling)
        {
            var dataType = SelectedSettlingVendorOption?.Handler ?? SettlingDataTypeRegistry.Get(null);
            double band = ParseSpecNumber(SettlingSpec.ToleranceBand) ?? 0;
            double tailFraction = (ParseSpecNumber(SettlingSpec.TailWindowPercent) ?? 20) / 100.0;

            var grouped = item.SettlingGroups.Select(g => (g.GroupLabel, Series: g.Series.ToList())).ToList();
            var groupMetrics = BuildSettlingGroupMetrics(grouped, band, tailFraction, dataType);
            foreach (var group in groupMetrics)
            {
                string gpath = Path.Combine(plotDir, $"{stem}_{SanitizeFileName(group.GroupLabel)}.png");
                PlotImageExporter.SaveSettlingConditionPng(
                    gpath, group.GroupLabel, group.Series, group.Result, group.Displacement, group.ToleranceBand);
            }
            return groupMetrics.Count;
        }

        if (item.IsSquareTest)
        {
            int n = 0;
            foreach (var axis in item.SquareAxes)
            {
                string apath = Path.Combine(plotDir, $"{stem}_{SanitizeFileName(axis.Label)}_square.png");
                SaveSinewaveAxisPlotPng(axis.TimeMs, axis.Target, axis.Value, apath, $"{stem} - {axis.Label}");
                n++;
            }
            return n;
        }

        if (item.IsSinewave)
        {
            SaveSinewaveAxisPlotPng(item.SinewaveTimeMs, item.SinewavePosiX, item.SinewaveHallX,
                Path.Combine(plotDir, $"{stem}_X.png"), $"{stem} - X-axis");
            SaveSinewaveAxisPlotPng(item.SinewaveTimeMs, item.SinewavePosiY, item.SinewaveHallY,
                Path.Combine(plotDir, $"{stem}_Y.png"), $"{stem} - Y-axis");
            return 2;
        }

        if (item.IsCircleTest)
        {
            SaveCirclePlotPng(item.CircleReadVtX, item.CircleReadVtY, item.CircleHallX, item.CircleHallY,
                Path.Combine(plotDir, $"{stem}_circle.png"), stem);
            return 1;
        }

        if (item.IsTilt)
        {
            double? specTilt = ParseSpecNumber(TiltSpec.SpecTilt);
            SaveTiltPlotPng(item.TiltUpTx, item.TiltUpTy, item.TiltDownTx, item.TiltDownTy,
                Path.Combine(plotDir, $"{stem}_tilt.png"), stem, specTilt);
            return 1;
        }

        if (item.IsScan)
        {
            // SCAN 그래프에는 range별 지표 주석이 들어가므로, Export와 같은 그림이 나오도록
            // 원본 파일을 다시 읽어 지표를 계산한다(PlotFileItem에는 지표가 없다).
            var dataType = ScanDataTypeRegistry.Get(ScanSpec.SelectedDataType);
            var read = dataType.Read(
                item.FullPath,
                SplitCandidates(ScanSpec.InputCandidatesText),
                SplitCandidates(ScanSpec.OutputCandidatesText));
            var ranges = ResolveScanRanges(read, ParseScanRanges(ScanSpec.RangesText).ToList(), dataType);
            var metrics = ranges.Select(r => dataType.ComputeMetrics(read, r)).ToList();
            var refLine = ranges.Count > 0
                ? dataType.BuildReferenceLine(read, ranges[0])
                : (Array.Empty<double>(), Array.Empty<double>());

            SaveScanPlotPng(
                read.UpInput, read.UpOutput, read.DownInput, read.DownOutput,
                refLine.Item1, refLine.Item2,
                Path.Combine(plotDir, $"{stem}_scan.png"),
                stem, read.InputColumn, read.OutputColumn, metrics,
                useThousandYTicks: string.Equals(dataType.Name, "AKM", StringComparison.OrdinalIgnoreCase));
            return 1;
        }

        if (item.IsDcSensitivity)
        {
            SaveDcPlotPng(item.DcUpInput, item.DcUpOutput, item.DcDownInput, item.DcDownOutput,
                Path.Combine(plotDir, $"{stem}.png"), stem, "Input", "Output");
            return 1;
        }

        // 나머지(FRA)는 Bode 그래프를 파일당 한 장으로 저장한다.
        if (item.Points.Count > 0)
        {
            var series = new List<(string name, IReadOnlyList<BodePoint> pts)>
            {
                (stem, (IReadOnlyList<BodePoint>)ApplyFraPhaseMode(item.Points)),
            };
            PlotImageExporter.SaveBodeOverlayPng(series, Path.Combine(plotDir, $"{stem}.png"));
            return 1;
        }

        throw new InvalidOperationException("이 파일에는 저장할 그래프 데이터가 없습니다.");
    }

    // DC SENSITIVITY: 체크한 파일 전체를 한 장에 겹쳐 저장한다.
    private void SaveSelectedDcOverlay()
    {
        var selected = PlotFiles
            .Where(x => x.IsOverlaySelected && !x.IsAllOverlayItem && x.IsDcSensitivity)
            .ToList();

        if (selected.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "파일 목록에서 겹쳐 그릴 파일을 체크하세요.",
                "DC 오버레이 저장",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        string plotDir = EnsureSelectedPlotDir();
        string label = string.IsNullOrWhiteSpace(SelectedOverlayExportLabel)
            ? "overlay"
            : SanitizeFileName(SelectedOverlayExportLabel.Trim());
        string path = Path.Combine(plotDir, $"DC_{label}_{selected.Count}files.png");

        var series = selected
            .Select(x => (
                Name: Path.GetFileNameWithoutExtension(x.FileName),
                UpX: x.DcUpInput,
                UpY: x.DcUpOutput,
                DownX: x.DcDownInput,
                DownY: x.DcDownOutput))
            .ToList();

        SaveDcOverlayPng(series, path, $"DC Sensitivity Overlay ({selected.Count} files)", "Input", "Output");

        ExportOutputLabel = $"DC 오버레이 저장 완료: {path}";
        System.Windows.MessageBox.Show(
            $"{selected.Count}개 파일을 한 장에 저장했습니다.{Environment.NewLine}{path}",
            "DC 오버레이 저장",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private List<BodePoint> ApplyFraPhaseMode(IReadOnlyList<BodePoint> points)
    {
        if (!IsFraPhaseInverted)
            return points.OrderBy(p => p.FrequencyHz).ToList();

        return points
            .Select(p => p with { PhaseDeg = -p.PhaseDeg })
            .OrderBy(p => p.FrequencyHz)
            .ToList();
    }

    public double GetDisplayFraPhase(double phaseDeg)
        => IsFraPhaseInverted ? -phaseDeg : phaseDeg;

    private void ApplyScanJudgementToAll()
    {
        var map = new Dictionary<string, (string Judge, string Reason)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ScanResults.Count; i++)
        {
            var row = ScanResults[i];
            var (judge, reason) = JudgeScanBySpec(row);
            map[row.FileName] = (judge, reason);

            if (row.Judge == judge && row.JudgeReason == reason)
                continue;

            ScanResults[i] = new ScanResultRow
            {
                FileName = row.FileName,
                HeaderRow = row.HeaderRow,
                InputColumn = row.InputColumn,
                OutputColumn = row.OutputColumn,
                UpCount = row.UpCount,
                DownCount = row.DownCount,
                Range1 = row.Range1,
                Hys1 = row.Hys1,
                Li1 = row.Li1,
                Range2 = row.Range2,
                Hys2 = row.Hys2,
                Li2 = row.Li2,
                Judge = judge,
                JudgeReason = reason,
                PlotPath = row.PlotPath
            };
        }

        foreach (var pf in PlotFiles)
        {
            if (map.TryGetValue(pf.FileName, out var v))
            {
                pf.Judge = v.Judge;
                pf.JudgeReason = v.Reason;
            }
            else
            {
                pf.Judge = "";
                pf.JudgeReason = "";
            }
        }

        if (SelectedScanRow is not null)
        {
            var selected = ScanResults.FirstOrDefault(x => x.FileName == SelectedScanRow.FileName);
            if (selected is not null)
                SelectedScanRow = selected;
        }

        RefreshPlotFilter();
    }

    private (string judge, string reason) JudgeScanBySpec(ScanResultRow row)
    {
        var reasons = new List<string>();

        double? specHys1 = ParseSpecNumber(ScanSpec.SpecHys1);
        double? specLi1 = ParseSpecNumber(ScanSpec.SpecLi1);
        bool hasRange1 = !string.IsNullOrWhiteSpace(row.Range1);

        if (hasRange1 && specHys1 is double hys1Spec)
        {
            if (row.Hys1 is double hys1)
            {
                if (Math.Abs(hys1) > Math.Abs(hys1Spec))
                    reasons.Add($"|HYS1|>{Math.Abs(hys1Spec):0.###}");
            }
            else reasons.Add("HYS1=N/A");
        }

        if (hasRange1 && specLi1 is double li1Spec)
        {
            if (row.Li1 is double li1)
            {
                if (Math.Abs(li1) > Math.Abs(li1Spec))
                    reasons.Add($"|LI1|>{Math.Abs(li1Spec):0.###}");
            }
            else reasons.Add("LI1=N/A");
        }

        return reasons.Count == 0 ? ("PASS", "") : ("NG", string.Join(", ", reasons));
    }

    private void ApplySinewaveJudgementToAll()
    {
        var map = new Dictionary<string, (string Judge, string Reason)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < SinewaveResults.Count; i++)
        {
            var row = SinewaveResults[i];
            var (judge, reason) = JudgeSinewaveBySpec(row);
            map[row.FileName] = (judge, reason);

            if (row.Judge == judge && row.JudgeReason == reason)
                continue;

            SinewaveResults[i] = new SinewaveResultRow
            {
                FileName = row.FileName,
                SampleCount = row.SampleCount,
                MaxErrX = row.MaxErrX,
                MaxErrY = row.MaxErrY,
                Judge = judge,
                JudgeReason = reason,
                PlotPathX = row.PlotPathX,
                PlotPathY = row.PlotPathY
            };
        }

        foreach (var pf in PlotFiles)
        {
            if (map.TryGetValue(pf.FileName, out var v))
            {
                pf.Judge = v.Judge;
                pf.JudgeReason = v.Reason;
            }
            else
            {
                pf.Judge = "";
                pf.JudgeReason = "";
            }
        }

        if (SelectedSinewaveRow is not null)
        {
            var selected = SinewaveResults.FirstOrDefault(x => x.FileName == SelectedSinewaveRow.FileName);
            if (selected is not null)
                SelectedSinewaveRow = selected;
        }

        RefreshPlotFilter();
    }

    private (string judge, string reason) JudgeSinewaveBySpec(SinewaveResultRow row)
    {
        var reasons = new List<string>();
        double? specMaxErr = ParseSpecNumber(SinewaveSpec.SpecMaxErr);

        if (specMaxErr is double maxErrSpec)
        {
            if (row.MaxErrX is double maxErrX && Math.Abs(maxErrX) > Math.Abs(maxErrSpec))
                reasons.Add($"X:|MaxErr|>{Math.Abs(maxErrSpec):0.###}");

            if (row.MaxErrY is double maxErrY && Math.Abs(maxErrY) > Math.Abs(maxErrSpec))
                reasons.Add($"Y:|MaxErr|>{Math.Abs(maxErrSpec):0.###}");
        }

        return reasons.Count == 0 ? ("PASS", "") : ("NG", string.Join(", ", reasons));
    }

    private void ApplyCircleJudgementToAll()
    {
        var map = new Dictionary<string, (string Judge, string Reason)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < CircleResults.Count; i++)
        {
            var row = CircleResults[i];
            var (judge, reason) = JudgeCircleBySpec(row);
            map[row.FileName] = (judge, reason);

            if (row.Judge == judge && row.JudgeReason == reason)
                continue;

            CircleResults[i] = new CircleResultRow
            {
                FileName = row.FileName,
                SampleCount = row.SampleCount,
                MaxErrX = row.MaxErrX,
                MaxErrY = row.MaxErrY,
                Judge = judge,
                JudgeReason = reason,
                PlotPathX = row.PlotPathX,
                PlotPathY = row.PlotPathY
            };
        }

        foreach (var pf in PlotFiles)
        {
            if (map.TryGetValue(pf.FileName, out var v))
            {
                pf.Judge = v.Judge;
                pf.JudgeReason = v.Reason;
            }
            else
            {
                pf.Judge = "";
                pf.JudgeReason = "";
            }
        }

        if (SelectedCircleRow is not null)
        {
            var selected = CircleResults.FirstOrDefault(x => x.FileName == SelectedCircleRow.FileName);
            if (selected is not null)
                SelectedCircleRow = selected;
        }

        RefreshPlotFilter();
    }

    private (string judge, string reason) JudgeCircleBySpec(CircleResultRow row)
    {
        var reasons = new List<string>();
        double? specMaxErr = ParseSpecNumber(CircleSpec.SpecMaxErr);

        if (specMaxErr is double maxErrSpec)
        {
            if (row.MaxErrX is double maxErrX && Math.Abs(maxErrX) > Math.Abs(maxErrSpec))
                reasons.Add($"X:|MaxErr|>{Math.Abs(maxErrSpec):0.###}");

            if (row.MaxErrY is double maxErrY && Math.Abs(maxErrY) > Math.Abs(maxErrSpec))
                reasons.Add($"Y:|MaxErr|>{Math.Abs(maxErrSpec):0.###}");
        }

        return reasons.Count == 0 ? ("PASS", "") : ("NG", string.Join(", ", reasons));
    }

    private void ApplySquareJudgementToAll()
    {
        var map = new Dictionary<string, (string Judge, string Reason)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < SquareResults.Count; i++)
        {
            var row = SquareResults[i];
            var (judge, reason) = JudgeSquareBySpec(row);
            map[row.FileName] = (judge, reason);

            if (row.Judge == judge && row.JudgeReason == reason)
                continue;

            SquareResults[i] = new SquareResultRow
            {
                FileName = row.FileName,
                SampleCount = row.SampleCount,
                MaxSettlingMs = row.MaxSettlingMs,
                MaxOvershoot = row.MaxOvershoot,
                MaxSettlingUpMs = row.MaxSettlingUpMs,
                MaxOvershootUp = row.MaxOvershootUp,
                MaxSettlingDownMs = row.MaxSettlingDownMs,
                MaxOvershootDown = row.MaxOvershootDown,
                Judge = judge,
                JudgeReason = reason,
                PlotPath = row.PlotPath
            };
        }

        foreach (var pf in PlotFiles)
        {
            if (map.TryGetValue(pf.FileName, out var v))
            {
                pf.Judge = v.Judge;
                pf.JudgeReason = v.Reason;
            }
            else
            {
                pf.Judge = "";
                pf.JudgeReason = "";
            }
        }

        if (SelectedSquareRow is not null)
        {
            var selected = SquareResults.FirstOrDefault(x => x.FileName == SelectedSquareRow.FileName);
            if (selected is not null)
                SelectedSquareRow = selected;
        }

        RefreshPlotFilter();
    }

    private (string judge, string reason) JudgeSquareBySpec(SquareResultRow row)
    {
        var reasons = new List<string>();
        double? specSettling = ParseSpecNumber(SquareSpec.SpecMaxSettlingTimeMs);
        double? specOvershoot = ParseSpecNumber(SquareSpec.SpecMaxOvershoot);

        if (specSettling is double settlingSpec)
        {
            if (row.MaxSettlingMs is double sc && Math.Abs(sc) > Math.Abs(settlingSpec))
                reasons.Add($"Settling>{Math.Abs(settlingSpec):0.###}ms");
            if (row.MaxSettlingUpMs is double su && Math.Abs(su) > Math.Abs(settlingSpec))
                reasons.Add($"Up:Settling>{Math.Abs(settlingSpec):0.###}ms");
            if (row.MaxSettlingDownMs is double sd && Math.Abs(sd) > Math.Abs(settlingSpec))
                reasons.Add($"Down:Settling>{Math.Abs(settlingSpec):0.###}ms");
        }

        if (specOvershoot is double overshootSpec)
        {
            if (row.MaxOvershoot is double oc && Math.Abs(oc) > Math.Abs(overshootSpec))
                reasons.Add($"Overshoot>{Math.Abs(overshootSpec):0.###}");
            if (row.MaxOvershootUp is double ou && Math.Abs(ou) > Math.Abs(overshootSpec))
                reasons.Add($"Up:Overshoot>{Math.Abs(overshootSpec):0.###}");
            if (row.MaxOvershootDown is double od && Math.Abs(od) > Math.Abs(overshootSpec))
                reasons.Add($"Down:Overshoot>{Math.Abs(overshootSpec):0.###}");
        }

        return reasons.Count == 0 ? ("PASS", "") : ("NG", string.Join(", ", reasons));
    }

    private void ApplyTiltJudgementToAll()
    {
        var map = new Dictionary<string, (string Judge, string Reason)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < TiltResults.Count; i++)
        {
            var row = TiltResults[i];
            var (judge, reason) = JudgeTiltBySpec(row);
            map[row.FileName] = (judge, reason);

            if (row.Judge == judge && row.JudgeReason == reason)
                continue;

            TiltResults[i] = new TiltResultRow
            {
                FileName = row.FileName,
                UpCount = row.UpCount,
                DownCount = row.DownCount,
                PeakTiltUp = row.PeakTiltUp,
                PeakTiltDown = row.PeakTiltDown,
                PeakTilt = row.PeakTilt,
                Judge = judge,
                JudgeReason = reason,
                PlotPath = row.PlotPath
            };
        }

        foreach (var pf in PlotFiles)
        {
            if (map.TryGetValue(pf.FileName, out var v))
            {
                pf.Judge = v.Judge;
                pf.JudgeReason = v.Reason;
            }
            else
            {
                pf.Judge = "";
                pf.JudgeReason = "";
            }
        }

        if (SelectedTiltRow is not null)
        {
            var selected = TiltResults.FirstOrDefault(x => x.FileName == SelectedTiltRow.FileName);
            if (selected is not null)
                SelectedTiltRow = selected;
        }

        RefreshPlotFilter();
    }

    private (string judge, string reason) JudgeTiltBySpec(TiltResultRow row)
    {
        var reasons = new List<string>();
        double? specTilt = ParseSpecNumber(TiltSpec.SpecTilt);

        if (specTilt is double tiltSpec)
        {
            if (row.PeakTiltUp is double peakUp && Math.Abs(peakUp) > Math.Abs(tiltSpec))
                reasons.Add($"|PeakUp|>{Math.Abs(tiltSpec):0.###}");

            if (row.PeakTiltDown is double peakDown && Math.Abs(peakDown) > Math.Abs(tiltSpec))
                reasons.Add($"|PeakDown|>{Math.Abs(tiltSpec):0.###}");
        }

        return reasons.Count == 0 ? ("PASS", "") : ("NG", string.Join(", ", reasons));
    }

    private void RecalculateFraResultsFromCurrentPlotData()
    {
        if (PlotFiles.Count == 0)
            return;

        var selectedFile = SelectedFraRow?.FileName;
        FraResults.Clear();

        foreach (var item in PlotFiles.Where(p => p.Points.Count > 0))
        {
            var processed = ApplyFraPhaseMode(item.Points);
            var metrics = BodeMetrics.Extract(processed, useCrossGuard: false);
            var gain10 = FraHelpers.GainNearestAtHz(item.Points, 10.0);

            FraResults.Add(new FraResultRow
            {
                FileName = item.FileName,
                Gain10Hz = gain10,
                GM = metrics.GainMarginDb,
                GM_Freq = metrics.GmFreqHz,
                PM = metrics.PhaseMarginDeg,
                PM_Freq = metrics.PmFreqHz,
                PM3 = metrics.PhaseMarginAtMinus3dBDeg,
                PM3_Freq = metrics.PmMinus3dBFreqHz,
            });
        }

        ApplyJudgementToAll();

        if (!string.IsNullOrWhiteSpace(selectedFile))
        {
            SelectedFraRow = FraResults.FirstOrDefault(x => x.FileName == selectedFile) ?? FraResults.FirstOrDefault();
        }
        else if (FraResults.Count > 0)
        {
            SelectedFraRow = FraResults[0];
        }

        UpdateRecentFromRow(SelectedFraRow);
    }

    private void UpdateSpecPanel()
    {
        ActiveSpec = SelectedInspection switch
        {
            "FRA" => FraSpec,
            "DC SENSITIVITY" => DcSensitivitySpec,
            "SCAN" => ScanSpec,
            "SETTLING TIME" => SettlingSpec,
            "TILT" => TiltSpec,
            "SINE WAVE" => SinewaveSpec,
            "CIRCLE TEST" => CircleSpec,
            "SQUARE TEST" => SquareSpec,
            _ => EmptySpec
        };
    }

    private void RefreshPlotFilter()
    {
        PlotFilesFiltered.Clear();
        var q = (PlotSearchText ?? "").Trim().ToLowerInvariant();
        var allOverlayItem = PlotFiles.FirstOrDefault(f => f.IsAllOverlayItem);
        if (allOverlayItem is not null)
            PlotFilesFiltered.Add(allOverlayItem);

        foreach (var f in PlotFiles.Where(x => !x.IsAllOverlayItem))
        {
            if (string.IsNullOrEmpty(q) || f.FileName.ToLowerInvariant().Contains(q))
                PlotFilesFiltered.Add(f);
        }

        if (SelectedPlotFile is not null && !PlotFilesFiltered.Contains(SelectedPlotFile))
            SelectedPlotFile = PlotFilesFiltered.FirstOrDefault(x => !x.IsAllOverlayItem) ?? PlotFilesFiltered.FirstOrDefault();
        else if (SelectedPlotFile is null && PlotFilesFiltered.Count > 0)
            SelectedPlotFile = PlotFilesFiltered.FirstOrDefault(x => !x.IsAllOverlayItem) ?? PlotFilesFiltered[0];
    }

    private void SyncSelectionToPlot()
    {
        string? target = SelectedInspection switch
        {
            "DC SENSITIVITY" => SelectedDcRow?.FileName,
            "SCAN" => SelectedScanRow?.FileName,
            "SETTLING TIME" => SelectedSettlingRow?.FileName,
            "TILT" => SelectedTiltRow?.FileName,
            "SINE WAVE" => SelectedSinewaveRow?.FileName,
            "CIRCLE TEST" => SelectedCircleRow?.FileName,
            "SQUARE TEST" => SelectedSquareRow?.FileName,
            _ => SelectedFraRow?.FileName,
        };
        if (string.IsNullOrWhiteSpace(target)) return;
        var match = PlotFilesFiltered.FirstOrDefault(p => p.FileName == target)
                    ?? PlotFiles.FirstOrDefault(p => p.FileName == target);
        if (match is not null && !ReferenceEquals(match, SelectedPlotFile))
            SelectedPlotFile = match;
    }

    private void SyncSelectionToTable()
    {
        if (SelectedPlotFile is null || SelectedPlotFile.IsAllOverlayItem) return;

        if (IsDcSensitivityInspection)
        {
            var dc = DcResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (dc is not null && !ReferenceEquals(dc, SelectedDcRow))
                SelectedDcRow = dc;
        }
        else if (IsScanInspection)
        {
            var scan = ScanResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (scan is not null && !ReferenceEquals(scan, SelectedScanRow))
                SelectedScanRow = scan;
        }
        else if (IsSettlingInspection)
        {
            var settling = SettlingResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (settling is not null && !ReferenceEquals(settling, SelectedSettlingRow))
                SelectedSettlingRow = settling;
        }
        else if (IsTiltInspection)
        {
            var tilt = TiltResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (tilt is not null && !ReferenceEquals(tilt, SelectedTiltRow))
                SelectedTiltRow = tilt;
        }
        else if (IsSinewaveInspection)
        {
            var sinewave = SinewaveResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (sinewave is not null && !ReferenceEquals(sinewave, SelectedSinewaveRow))
                SelectedSinewaveRow = sinewave;
        }
        else if (IsCircleInspection)
        {
            var circle = CircleResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (circle is not null && !ReferenceEquals(circle, SelectedCircleRow))
                SelectedCircleRow = circle;
        }
        else if (IsSquareInspection)
        {
            var square = SquareResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (square is not null && !ReferenceEquals(square, SelectedSquareRow))
                SelectedSquareRow = square;
        }
        else
        {
            var fra = FraResults.FirstOrDefault(r => r.FileName == SelectedPlotFile.FileName);
            if (fra is null)
                return;

            if (!ReferenceEquals(fra, SelectedFraRow))
                SelectedFraRow = fra;
            else
                UpdateRecentFromRow(fra);
        }
    }

    private static IEnumerable<string> SplitCandidates(string text)
        => (text ?? string.Empty)
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));

    private static IEnumerable<ScanRangeDefinition> ParseScanRanges(string text)
    {
        foreach (var token in (text ?? string.Empty)
                     .Split(new[] { ',', ';', '/', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                continue;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var min)
                && !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out min))
                continue;

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var max)
                && !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.CurrentCulture, out max))
                continue;

            if (max < min)
                (min, max) = (max, min);

            if (Math.Abs(max - min) < 1e-12)
                continue;

            yield return new ScanRangeDefinition(min, max);
        }
    }

    private static string FormatRange(double min, double max)
        => $"{min:0.###}-{max:0.###}";

    private static double? ParseSpecNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string s = text.Trim();
        s = s.Replace("±", "", StringComparison.Ordinal);
        s = s.Replace("+/-", "", StringComparison.Ordinal);
        s = s.Replace("+-", "", StringComparison.Ordinal);
        s = s.Replace("-+", "", StringComparison.Ordinal);
        s = s.Replace("_", "", StringComparison.Ordinal);
        s = s.Replace(" ", "", StringComparison.Ordinal);

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
            return v;

        return null;
    }

    private static bool IsFraJudgementSpecProperty(string? propertyName)
        => propertyName is nameof(FraSpecViewModel.GainMarginDb)
            or nameof(FraSpecViewModel.PhaseMarginDeg)
            or nameof(FraSpecViewModel.PhaseMarginMinus3dBDeg);

    private static bool IsScanJudgementSpecProperty(string? propertyName)
        => propertyName is nameof(ScanSpecViewModel.SpecHys1)
            or nameof(ScanSpecViewModel.SpecLi1);

    private static bool IsTiltJudgementSpecProperty(string? propertyName)
        => propertyName is nameof(TiltSpecViewModel.SpecTilt);

    private static bool IsSinewaveJudgementSpecProperty(string? propertyName)
        => propertyName is nameof(SinewaveSpecViewModel.SpecMaxErr);

    private static bool IsCircleJudgementSpecProperty(string? propertyName)
        => propertyName is nameof(CircleTestSpecViewModel.SpecMaxErr);

    private static bool IsSquareJudgementSpecProperty(string? propertyName)
        => propertyName is nameof(SquareTestSpecViewModel.SpecMaxSettlingTimeMs) or nameof(SquareTestSpecViewModel.SpecMaxOvershoot);

    private static DataTable TakePreview(DataTable source, int rows)
    {
        var preview = source.Clone();
        for (int i = 0; i < Math.Min(rows, source.Rows.Count); i++)
            preview.ImportRow(source.Rows[i]);
        return preview;
    }

    private static DataTable BuildScanSummaryTable(IEnumerable<ScanResultRow> rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("FileName", typeof(string));
        dt.Columns.Add("Judge", typeof(string));
        dt.Columns.Add("Reason", typeof(string));
        dt.Columns.Add("HeaderRow", typeof(int));
        dt.Columns.Add("InputColumn", typeof(string));
        dt.Columns.Add("OutputColumn", typeof(string));
        dt.Columns.Add("UpCount", typeof(int));
        dt.Columns.Add("DownCount", typeof(int));
        dt.Columns.Add("Range1", typeof(string));
        dt.Columns.Add("Hys1", typeof(double));
        dt.Columns.Add("LI1", typeof(double));
        dt.Columns.Add("Range2", typeof(string));
        dt.Columns.Add("Hys2", typeof(double));
        dt.Columns.Add("LI2", typeof(double));

        foreach (var r in rows)
        {
            var row = dt.NewRow();
            row["FileName"] = r.FileName;
            row["Judge"] = r.Judge;
            row["Reason"] = r.JudgeReason;
            row["HeaderRow"] = r.HeaderRow;
            row["InputColumn"] = r.InputColumn;
            row["OutputColumn"] = r.OutputColumn;
            row["UpCount"] = r.UpCount;
            row["DownCount"] = r.DownCount;
            row["Range1"] = r.Range1;
            row["Hys1"] = r.Hys1 is null ? DBNull.Value : r.Hys1.Value;
            row["LI1"] = r.Li1 is null ? DBNull.Value : r.Li1.Value;
            row["Range2"] = r.Range2;
            row["Hys2"] = r.Hys2 is null ? DBNull.Value : r.Hys2.Value;
            row["LI2"] = r.Li2 is null ? DBNull.Value : r.Li2.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    private static DataTable BuildTiltSummaryTable(IEnumerable<TiltResultRow> rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("FileName", typeof(string));
        dt.Columns.Add("Judge", typeof(string));
        dt.Columns.Add("Reason", typeof(string));
        dt.Columns.Add("UpCount", typeof(int));
        dt.Columns.Add("DownCount", typeof(int));
        dt.Columns.Add("PeakTiltUp", typeof(double));
        dt.Columns.Add("PeakTiltDown", typeof(double));
        dt.Columns.Add("PeakTilt", typeof(double));

        foreach (var r in rows)
        {
            var row = dt.NewRow();
            row["FileName"] = r.FileName;
            row["Judge"] = r.Judge;
            row["Reason"] = r.JudgeReason;
            row["UpCount"] = r.UpCount;
            row["DownCount"] = r.DownCount;
            row["PeakTiltUp"] = r.PeakTiltUp is null ? DBNull.Value : r.PeakTiltUp.Value;
            row["PeakTiltDown"] = r.PeakTiltDown is null ? DBNull.Value : r.PeakTiltDown.Value;
            row["PeakTilt"] = r.PeakTilt is null ? DBNull.Value : r.PeakTilt.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    private static DataTable BuildSinewaveSummaryTable(IEnumerable<SinewaveResultRow> rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("FileName", typeof(string));
        dt.Columns.Add("Judge", typeof(string));
        dt.Columns.Add("Reason", typeof(string));
        dt.Columns.Add("SampleCount", typeof(int));
        dt.Columns.Add("MaxErrX", typeof(double));
        dt.Columns.Add("MaxErrY", typeof(double));

        foreach (var r in rows)
        {
            var row = dt.NewRow();
            row["FileName"] = r.FileName;
            row["Judge"] = r.Judge;
            row["Reason"] = r.JudgeReason;
            row["SampleCount"] = r.SampleCount;
            row["MaxErrX"] = r.MaxErrX is null ? DBNull.Value : r.MaxErrX.Value;
            row["MaxErrY"] = r.MaxErrY is null ? DBNull.Value : r.MaxErrY.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    private static DataTable BuildSquareSummaryTable(IEnumerable<SquareResultRow> rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("FileName", typeof(string));
        dt.Columns.Add("Judge", typeof(string));
        dt.Columns.Add("Reason", typeof(string));
        dt.Columns.Add("SampleCount", typeof(int));
        dt.Columns.Add("MaxSettlingMs", typeof(double));
        dt.Columns.Add("MaxOvershoot", typeof(double));
        dt.Columns.Add("MaxSettlingUpMs", typeof(double));
        dt.Columns.Add("MaxOvershootUp", typeof(double));
        dt.Columns.Add("MaxSettlingDownMs", typeof(double));
        dt.Columns.Add("MaxOvershootDown", typeof(double));

        foreach (var r in rows)
        {
            var row = dt.NewRow();
            row["FileName"] = r.FileName;
            row["Judge"] = r.Judge;
            row["Reason"] = r.JudgeReason;
            row["SampleCount"] = r.SampleCount;
            row["MaxSettlingMs"] = r.MaxSettlingMs is null ? DBNull.Value : r.MaxSettlingMs.Value;
            row["MaxOvershoot"] = r.MaxOvershoot is null ? DBNull.Value : r.MaxOvershoot.Value;
            row["MaxSettlingUpMs"] = r.MaxSettlingUpMs is null ? DBNull.Value : r.MaxSettlingUpMs.Value;
            row["MaxOvershootUp"] = r.MaxOvershootUp is null ? DBNull.Value : r.MaxOvershootUp.Value;
            row["MaxSettlingDownMs"] = r.MaxSettlingDownMs is null ? DBNull.Value : r.MaxSettlingDownMs.Value;
            row["MaxOvershootDown"] = r.MaxOvershootDown is null ? DBNull.Value : r.MaxOvershootDown.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    private static DataTable BuildCircleSummaryTable(IEnumerable<CircleResultRow> rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("FileName", typeof(string));
        dt.Columns.Add("Judge", typeof(string));
        dt.Columns.Add("Reason", typeof(string));
        dt.Columns.Add("SampleCount", typeof(int));
        dt.Columns.Add("MaxErrX", typeof(double));
        dt.Columns.Add("MaxErrY", typeof(double));

        foreach (var r in rows)
        {
            var row = dt.NewRow();
            row["FileName"] = r.FileName;
            row["Judge"] = r.Judge;
            row["Reason"] = r.JudgeReason;
            row["SampleCount"] = r.SampleCount;
            row["MaxErrX"] = r.MaxErrX is null ? DBNull.Value : r.MaxErrX.Value;
            row["MaxErrY"] = r.MaxErrY is null ? DBNull.Value : r.MaxErrY.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    private static void SaveDataTableToCsv(DataTable dt, string path)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => Esc(c.ColumnName))));
        foreach (DataRow row in dt.Rows)
        {
            var values = dt.Columns.Cast<DataColumn>().Select(c =>
            {
                var v = row[c];
                if (v == DBNull.Value || v is null) return "";
                return v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) ?? "" : v.ToString() ?? "";
            });
            sw.WriteLine(string.Join(",", values.Select(Esc)));
        }
    }

    private static void SaveDcPlotPng(
        IReadOnlyList<double> upX,
        IReadOnlyList<double> upY,
        IReadOnlyList<double> downX,
        IReadOnlyList<double> downY,
        string path,
        string title,
        string xLabel,
        string yLabel)
    {
        using var bmp = new System.Drawing.Bitmap(1400, 900);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int plotMargin = 100;
        var plotRect = new System.Drawing.Rectangle(
            plotMargin,
            plotMargin,
            bmp.Width - (plotMargin * 2),
            bmp.Height - (plotMargin * 2));
        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.2f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(230, 230, 230), 1f);
        using var upPen = new System.Drawing.Pen(System.Drawing.Color.Orange, 2.2f);
        using var downPen = new System.Drawing.Pen(System.Drawing.Color.DodgerBlue, 2.2f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        var allX = upX.Concat(downX).ToList();
        var allY = upY.Concat(downY).ToList();
        if (allX.Count == 0 || allY.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double minX = allX.Min();
        double maxX = allX.Max();
        double minY = allY.Min();
        double maxY = allY.Max();
        if (Math.Abs(maxX - minX) < 1e-9) { minX -= 1; maxX += 1; }
        if (Math.Abs(maxY - minY) < 1e-9) { minY -= 1; maxY += 1; }

        var xTicks = BuildNiceTicks(minX, maxX, 6);
        var yTicks = BuildNiceTicks(minY, maxY, 6);
        minX = xTicks.Min();
        maxX = xTicks.Max();
        minY = yTicks.Min();
        maxY = yTicks.Max();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = plotRect.Left + (plotRect.Width - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        foreach (var yt in yTicks)
        {
            float y = MapY(yt, minY, maxY, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = FormatTick(yt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, plotRect.Left - size.Width - 12, y - size.Height / 2);
        }
        foreach (var xt in xTicks)
        {
            float x = MapX(xt, minX, maxX, plotRect);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);
            var label = FormatTick(xt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - size.Width / 2, plotRect.Bottom + 10);
        }

        g.DrawRectangle(axisPen, plotRect);
        DrawSeries(g, upPen, upX, upY, plotRect, minX, maxX, minY, maxY);
        DrawSeries(g, downPen, downX, downY, plotRect, minX, maxX, minY, maxY);

        var xSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xSize.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        float lx = plotRect.Right - 170;
        float ly = plotRect.Top + 15;
        g.DrawLine(upPen, lx, ly + 8, lx + 28, ly + 8);
        g.DrawString("Up", legendFont, textBrush, lx + 36, ly);
        g.DrawLine(downPen, lx, ly + 34, lx + 28, ly + 34);
        g.DrawString("Down", legendFont, textBrush, lx + 36, ly + 26);

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    // 체크한 DC SENSITIVITY 파일 여러 개를 한 장에 겹쳐 그린다.
    // 파일마다 색을 바꾸고 Up은 실선, Down은 점선으로 구분한다.
    private static readonly System.Drawing.Color[] OverlayPalette =
    [
        System.Drawing.Color.FromArgb(0, 114, 189),
        System.Drawing.Color.FromArgb(217, 83, 25),
        System.Drawing.Color.FromArgb(237, 177, 32),
        System.Drawing.Color.FromArgb(126, 47, 142),
        System.Drawing.Color.FromArgb(119, 172, 48),
        System.Drawing.Color.FromArgb(77, 190, 238),
        System.Drawing.Color.FromArgb(162, 20, 47),
        System.Drawing.Color.FromArgb(70, 70, 70),
    ];

    private static void SaveDcOverlayPng(
        IReadOnlyList<(string Name, IReadOnlyList<double> UpX, IReadOnlyList<double> UpY, IReadOnlyList<double> DownX, IReadOnlyList<double> DownY)> series,
        string path,
        string title,
        string xLabel,
        string yLabel)
    {
        using var bmp = new System.Drawing.Bitmap(1400, 900);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int plotMargin = 100;
        var plotRect = new System.Drawing.Rectangle(
            plotMargin, plotMargin, bmp.Width - (plotMargin * 2), bmp.Height - (plotMargin * 2));

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.2f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(230, 230, 230), 1f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 9);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        var allX = series.SelectMany(s => s.UpX.Concat(s.DownX)).ToList();
        var allY = series.SelectMany(s => s.UpY.Concat(s.DownY)).ToList();
        if (allX.Count == 0 || allY.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double minX = allX.Min(), maxX = allX.Max();
        double minY = allY.Min(), maxY = allY.Max();
        if (Math.Abs(maxX - minX) < 1e-9) { minX -= 1; maxX += 1; }
        if (Math.Abs(maxY - minY) < 1e-9) { minY -= 1; maxY += 1; }

        var xTicks = BuildNiceTicks(minX, maxX, 6);
        var yTicks = BuildNiceTicks(minY, maxY, 6);
        minX = xTicks.Min(); maxX = xTicks.Max();
        minY = yTicks.Min(); maxY = yTicks.Max();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim();
            var ts = g.MeasureString(t, titleFont);
            g.DrawString(t, titleFont, textBrush, plotRect.Left + (plotRect.Width - ts.Width) / 2f, 18);
        }

        foreach (var yt in yTicks)
        {
            float y = MapY(yt, minY, maxY, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = FormatTick(yt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, plotRect.Left - size.Width - 12, y - size.Height / 2);
        }
        foreach (var xt in xTicks)
        {
            float x = MapX(xt, minX, maxX, plotRect);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);
            var label = FormatTick(xt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - size.Width / 2, plotRect.Bottom + 10);
        }

        g.DrawRectangle(axisPen, plotRect);

        for (int i = 0; i < series.Count; i++)
        {
            var color = OverlayPalette[i % OverlayPalette.Length];
            using var upPen = new System.Drawing.Pen(color, 2.0f);
            using var downPen = new System.Drawing.Pen(color, 2.0f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
            };
            DrawSeries(g, upPen, series[i].UpX, series[i].UpY, plotRect, minX, maxX, minY, maxY);
            DrawSeries(g, downPen, series[i].DownX, series[i].DownY, plotRect, minX, maxX, minY, maxY);
        }

        var xs = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xs.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ys = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ys.Width / 2f, 0);
        g.ResetTransform();

        // 범례: 파일명 (실선=Up, 점선=Down)
        float lx = plotRect.Left + 14;
        float ly = plotRect.Top + 12;
        g.DrawString("실선 = Up,  점선 = Down", legendFont, textBrush, lx, ly);
        ly += 18;
        for (int i = 0; i < series.Count; i++)
        {
            var color = OverlayPalette[i % OverlayPalette.Length];
            using var pen = new System.Drawing.Pen(color, 2.4f);
            g.DrawLine(pen, lx, ly + 7, lx + 26, ly + 7);
            g.DrawString(series[i].Name, legendFont, textBrush, lx + 32, ly);
            ly += 17;
            if (ly > plotRect.Bottom - 20) break;
        }

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void SaveScanPlotPng(
        IReadOnlyList<double> upX,
        IReadOnlyList<double> upY,
        IReadOnlyList<double> downX,
        IReadOnlyList<double> downY,
        IReadOnlyList<double> refX,
        IReadOnlyList<double> refY,
        string path,
        string title,
        string xLabel,
        string yLabel,
        IReadOnlyList<ScanRangeMetrics> metrics,
        bool useThousandYTicks = false)
    {
        using var bmp = new System.Drawing.Bitmap(1400, 900);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int plotMargin = 100;
        var plotRect = new System.Drawing.Rectangle(
            plotMargin,
            plotMargin,
            bmp.Width - (plotMargin * 2),
            bmp.Height - (plotMargin * 2));

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.1f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 232, 232), 1f);
        using var upPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 138, 33), 1.1f);
        using var downPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(42, 127, 239), 1.1f);
        using var refPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 80, 80), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
        using var subTextBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(70, 70, 70));

        var allX = upX.Concat(downX).Concat(refX).ToList();
        var allY = upY.Concat(downY).Concat(refY).ToList();
        if (allX.Count == 0 || allY.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double maxX = allX.Max();
        double minY = allY.Min();
        double maxY = allY.Max();
        double minX = 0;
        if (maxX <= 0 || double.IsNaN(maxX) || double.IsInfinity(maxX)) maxX = 100;
        if (Math.Abs(maxY - minY) < 1e-9) { minY -= 1; maxY += 1; }

        if (useThousandYTicks)
        {
            minY = 0;
            if (maxY <= 0 || double.IsNaN(maxY) || double.IsInfinity(maxY))
                maxY = 100;
        }
        else
        {
            minY = 0;
            if (maxY <= 0 || double.IsNaN(maxY) || double.IsInfinity(maxY))
            {
                maxY = 100;
            }
            else
            {
                maxY = Math.Ceiling(maxY / 100.0) * 100.0;
                if (maxY <= 0)
                    maxY = 100;
            }
        }

        var xTicks = BuildScanXTicks(maxX);
        var yTicks = useThousandYTicks ? BuildScanXTicks(maxY) : BuildTicksWithinRange(minY, maxY, 6);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = plotRect.Left + (plotRect.Width - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        foreach (var yt in yTicks)
        {
            float y = MapY(yt, minY, maxY, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = FormatTick(yt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, plotRect.Left - size.Width - 12, y - size.Height / 2);
        }

        foreach (var xt in xTicks)
        {
            float x = MapX(xt, minX, maxX, plotRect);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);
            var label = FormatTick(xt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - size.Width / 2, plotRect.Bottom + 10);
        }

        g.DrawRectangle(axisPen, plotRect);
        DrawSeries(g, refPen, refX, refY, plotRect, minX, maxX, minY, maxY, drawMarkers: false);
        DrawSeries(g, upPen, upX, upY, plotRect, minX, maxX, minY, maxY, drawMarkers: false);
        DrawSeries(g, downPen, downX, downY, plotRect, minX, maxX, minY, maxY, drawMarkers: false);

        var xSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xSize.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        float lx = plotRect.Right - 210;
        float ly = plotRect.Top + 15;
        g.DrawLine(refPen, lx, ly + 8, lx + 28, ly + 8);
        g.DrawString("Reference", legendFont, textBrush, lx + 36, ly);
        g.DrawLine(upPen, lx, ly + 34, lx + 28, ly + 34);
        g.DrawString("Up", legendFont, textBrush, lx + 36, ly + 26);
        g.DrawLine(downPen, lx, ly + 60, lx + 28, ly + 60);
        g.DrawString("Down", legendFont, textBrush, lx + 36, ly + 52);

        if (metrics.Count > 0)
        {
            float infoY = plotRect.Top + plotRect.Height - 24;
            string info = string.Join(" | ", metrics.Take(2).Select(m =>
                $"{FormatRange(m.MinInput, m.MaxInput)}  Hys={m.Hys:0.###}, LI={m.Linearity:0.###}"));
            g.DrawString(info, labelFont, subTextBrush, plotRect.Left + 4, infoY);
        }

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    // X축과 Y축을 각각 별도 이미지 한 장씩으로 그린다. Posi(target, 빨강 점선)와 Hall(실제, 파랑 실선)을
    // 같은 Y 스케일로 겹쳐 그려 두 값이 얼마나 일치하는지 한눈에 보이게 한다.
    private static void SaveSinewaveAxisPlotPng(
        IReadOnlyList<double> time,
        IReadOnlyList<double> posi,
        IReadOnlyList<double> hall,
        string path,
        string title)
    {
        using var bmp = new System.Drawing.Bitmap(1400, 700);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int marginLeft = 140;
        const int marginRight = 40;
        const int marginTop = 90;
        const int marginBottom = 90;
        int plotWidth = bmp.Width - marginLeft - marginRight;
        int plotHeight = bmp.Height - marginTop - marginBottom;
        var rect = new System.Drawing.Rectangle(marginLeft, marginTop, plotWidth, plotHeight);

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.2f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(230, 230, 230), 1f);
        using var posiPen = new System.Drawing.Pen(System.Drawing.Color.Red, 0.9f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var hallPen = new System.Drawing.Pen(System.Drawing.Color.Blue, 1.4f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 9);
        using var legendFont = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        if (time.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = marginLeft + (plotWidth - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        double minX = time.Min();
        double maxX = time.Max();
        if (Math.Abs(maxX - minX) < 1e-9) { minX -= 1; maxX += 1; }
        var xTicks = BuildNiceTicks(minX, maxX, 8);
        minX = xTicks.Min();
        maxX = xTicks.Max();

        var values = posi.Concat(hall).ToList();
        double minY = values.Count > 0 ? values.Min() : -1;
        double maxY = values.Count > 0 ? values.Max() : 1;
        if (Math.Abs(maxY - minY) < 1e-9) { minY -= 1; maxY += 1; }
        var yTicks = BuildNiceTicks(minY, maxY, 6);
        minY = yTicks.Min();
        maxY = yTicks.Max();

        float maxYTickLabelWidth = 0;
        foreach (var yt in yTicks)
        {
            float y = MapY(yt, minY, maxY, rect);
            g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
            var label = FormatTick(yt);
            var size = g.MeasureString(label, labelFont);
            maxYTickLabelWidth = Math.Max(maxYTickLabelWidth, size.Width);
            g.DrawString(label, labelFont, textBrush, rect.Left - size.Width - 12, y - size.Height / 2);
        }
        foreach (var xt in xTicks)
        {
            float x = MapX(xt, minX, maxX, rect);
            g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);
            var label = FormatTick(xt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - size.Width / 2, rect.Bottom + 10);
        }
        g.DrawRectangle(axisPen, rect);
        DrawSeries(g, posiPen, time, posi, rect, minX, maxX, minY, maxY, drawMarkers: false);
        DrawSeries(g, hallPen, time, hall, rect, minX, maxX, minY, maxY, drawMarkers: false);

        var yLabel = "Code";
        float yLabelOffsetX = maxYTickLabelWidth + 45;
        g.TranslateTransform(rect.Left - yLabelOffsetX, rect.Top + rect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        var xLabel = "time[ms]";
        var xLabelSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, rect.Left + rect.Width / 2f - xLabelSize.Width / 2f, rect.Bottom + 40);

        float legendX = rect.Right - 150;
        float legendY = rect.Top + 10;
        g.DrawLine(posiPen, legendX, legendY + 7, legendX + 24, legendY + 7);
        g.DrawString("Target", legendFont, textBrush, legendX + 30, legendY);
        g.DrawLine(hallPen, legendX, legendY + 27, legendX + 24, legendY + 27);
        g.DrawString("Hall", legendFont, textBrush, legendX + 30, legendY + 20);

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void SaveTiltPlotPng(
        IReadOnlyList<double> upTx,
        IReadOnlyList<double> upTy,
        IReadOnlyList<double> downTx,
        IReadOnlyList<double> downTy,
        string path,
        string title,
        double? specTilt)
    {
        using var bmp = new System.Drawing.Bitmap(1100, 1000);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int plotMargin = 100;
        int plotSize = Math.Min(bmp.Width, bmp.Height) - (plotMargin * 2);
        var plotRect = new System.Drawing.Rectangle(
            (bmp.Width - plotSize) / 2,
            plotMargin,
            plotSize,
            plotSize);

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.1f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 232, 232), 1f);
        using var crosshairPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 1f);
        using var specPen = new System.Drawing.Pen(System.Drawing.Color.IndianRed, 1.3f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var upPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 138, 33), 2.2f);
        using var downPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(42, 127, 239), 2.2f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        var allTx = upTx.Concat(downTx).ToList();
        var allTy = upTy.Concat(downTy).ToList();
        if (allTx.Count == 0 || allTy.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double maxAbs = allTx.Concat(allTy).Select(Math.Abs).DefaultIfEmpty(1).Max();
        if (specTilt is double specForRange)
            maxAbs = Math.Max(maxAbs, Math.Abs(specForRange));
        if (maxAbs <= 0 || double.IsNaN(maxAbs) || double.IsInfinity(maxAbs))
            maxAbs = 1;
        maxAbs *= 1.15;
        maxAbs = Math.Max(1, Math.Ceiling(maxAbs));

        double minX = -maxAbs, maxX = maxAbs, minY = -maxAbs, maxY = maxAbs;
        var ticks = new List<double>();
        for (double v = minX; v <= maxX + 0.5; v += 1)
            ticks.Add(v);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = plotRect.Left + (plotRect.Width - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        foreach (var t in ticks)
        {
            float y = MapY(t, minY, maxY, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            float x = MapX(t, minX, maxX, plotRect);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);

            var label = FormatTick(t);
            var sizeY = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, plotRect.Left - sizeY.Width - 12, y - sizeY.Height / 2);
            var sizeX = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - sizeX.Width / 2, plotRect.Bottom + 10);
        }

        g.DrawRectangle(axisPen, plotRect);

        float originX = MapX(0, minX, maxX, plotRect);
        float originY = MapY(0, minY, maxY, plotRect);
        g.DrawLine(crosshairPen, plotRect.Left, originY, plotRect.Right, originY);
        g.DrawLine(crosshairPen, originX, plotRect.Top, originX, plotRect.Bottom);

        if (specTilt is double specRadius && specRadius > 0)
        {
            float rx = MapX(specRadius, minX, maxX, plotRect) - originX;
            float ry = originY - MapY(specRadius, minY, maxY, plotRect);
            g.DrawEllipse(specPen, originX - rx, originY - ry, rx * 2, ry * 2);
        }

        DrawSeries(g, upPen, upTx, upTy, plotRect, minX, maxX, minY, maxY, drawMarkers: true);
        DrawSeries(g, downPen, downTx, downTy, plotRect, minX, maxX, minY, maxY, drawMarkers: true);

        const string xLabel = "Tx";
        const string yLabel = "Ty";
        var xSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xSize.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        float lx = plotRect.Right - 210;
        float ly = plotRect.Top + 15;
        g.DrawLine(upPen, lx, ly + 8, lx + 28, ly + 8);
        g.DrawString("Up", legendFont, textBrush, lx + 36, ly);
        g.DrawLine(downPen, lx, ly + 34, lx + 28, ly + 34);
        g.DrawString("Down", legendFont, textBrush, lx + 36, ly + 26);
        if (specTilt is not null)
        {
            g.DrawLine(specPen, lx, ly + 60, lx + 28, ly + 60);
            g.DrawString("Spec", legendFont, textBrush, lx + 36, ly + 52);
        }

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    // CIRCLE TEST: TILT의 4분면 산점도(SaveTiltPlotPng)와 동일한 방식이지만, 원점 대신 데이터 중심을
    // 기준으로 축 범위를 잡고 Up/Down 대신 Target(ReadVt) vs Hall을 겹쳐 그린다.
    private static void SaveCirclePlotPng(
        IReadOnlyList<double> readVtX,
        IReadOnlyList<double> readVtY,
        IReadOnlyList<double> hallX,
        IReadOnlyList<double> hallY,
        string path,
        string title)
    {
        using var bmp = new System.Drawing.Bitmap(1100, 1000);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int plotMargin = 100;
        int plotSize = Math.Min(bmp.Width, bmp.Height) - (plotMargin * 2);
        var plotRect = new System.Drawing.Rectangle(
            (bmp.Width - plotSize) / 2,
            plotMargin,
            plotSize,
            plotSize);

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.1f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 232, 232), 1f);
        using var crosshairPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 1f);
        using var targetPen = new System.Drawing.Pen(System.Drawing.Color.Red, 1.6f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var hallPen = new System.Drawing.Pen(System.Drawing.Color.Blue, 2.2f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        var allX = readVtX.Concat(hallX).ToList();
        var allY = readVtY.Concat(hallY).ToList();
        if (allX.Count == 0 || allY.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double centerX = (allX.Max() + allX.Min()) / 2.0;
        double centerY = (allY.Max() + allY.Min()) / 2.0;
        double radius = Math.Max(
            allX.Select(v => Math.Abs(v - centerX)).DefaultIfEmpty(1).Max(),
            allY.Select(v => Math.Abs(v - centerY)).DefaultIfEmpty(1).Max());
        if (radius <= 0 || double.IsNaN(radius) || double.IsInfinity(radius))
            radius = 1;
        radius *= 1.15;

        double minX = centerX - radius, maxX = centerX + radius, minY = centerY - radius, maxY = centerY + radius;
        double tickStep = Math.Max(1, Math.Round(radius / 4));
        var ticks = new List<double>();
        for (double v = Math.Ceiling(minX / tickStep) * tickStep; v <= maxX; v += tickStep)
            ticks.Add(v);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = plotRect.Left + (plotRect.Width - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        foreach (var t in ticks)
        {
            float y = MapY(t, minY, maxY, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            float x = MapX(t, minX, maxX, plotRect);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);

            var label = FormatTick(t);
            var sizeY = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, plotRect.Left - sizeY.Width - 12, y - sizeY.Height / 2);
            var sizeX = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - sizeX.Width / 2, plotRect.Bottom + 10);
        }

        g.DrawRectangle(axisPen, plotRect);

        float originX = MapX(centerX, minX, maxX, plotRect);
        float originY = MapY(centerY, minY, maxY, plotRect);
        g.DrawLine(crosshairPen, plotRect.Left, originY, plotRect.Right, originY);
        g.DrawLine(crosshairPen, originX, plotRect.Top, originX, plotRect.Bottom);

        DrawSeries(g, targetPen, readVtX, readVtY, plotRect, minX, maxX, minY, maxY, drawMarkers: false);
        DrawSeries(g, hallPen, hallX, hallY, plotRect, minX, maxX, minY, maxY, drawMarkers: false);

        const string xLabel = "X";
        const string yLabel = "Y";
        var xSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xSize.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        float lx = plotRect.Right - 210;
        float ly = plotRect.Top + 15;
        g.DrawLine(targetPen, lx, ly + 8, lx + 28, ly + 8);
        g.DrawString("Target", legendFont, textBrush, lx + 36, ly);
        g.DrawLine(hallPen, lx, ly + 34, lx + 28, ly + 34);
        g.DrawString("Hall", legendFont, textBrush, lx + 36, ly + 26);

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static (double min, double max) GetScanTightRange(double min, double max, double unit)
    {
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max) || unit <= 0)
            return (0, 100);

        if (max < min)
            (min, max) = (max, min);

        if (Math.Abs(max - min) < 1e-12)
        {
            min -= unit * 0.5;
            max += unit * 0.5;
        }

        double roundedMin = Math.Floor(min / unit) * unit;
        double roundedMax = Math.Ceiling(max / unit) * unit;
        if (roundedMax <= roundedMin)
            roundedMax = roundedMin + unit;

        return (roundedMin, roundedMax);
    }

    private static List<double> BuildScanXTicks(double max)
    {
        if (double.IsNaN(max) || double.IsInfinity(max) || max <= 0)
            return new List<double> { 0 };

        var ticks = new List<double>();
        for (double v = 0; v <= max + 1e-9; v += 1000)
            ticks.Add(v);

        if (ticks.Count == 0)
            ticks.Add(0);

        return ticks;
    }

    private static List<double> BuildTicksWithinRange(double min, double max, int targetCount)
    {
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max))
            return new List<double> { 0, 100 };

        if (max < min)
            (min, max) = (max, min);

        if (Math.Abs(max - min) < 1e-12)
            return new List<double> { Math.Round(min, 10) };

        double span = max - min;
        double step = NiceNumber(span / Math.Max(1, targetCount - 1), true);
        if (step <= 0 || double.IsNaN(step) || double.IsInfinity(step))
            step = 1;

        var ticks = new List<double>();
        double first = Math.Ceiling(min / step) * step;
        if (first > min + (step * 0.5))
            ticks.Add(Math.Round(min, 10));

        for (double v = first; v <= max + (step * 0.25); v += step)
        {
            var rounded = Math.Round(v, 10);
            if (rounded >= min - 1e-9 && rounded <= max + 1e-9)
                ticks.Add(rounded);
        }

        if (ticks.Count == 0 || Math.Abs(ticks[0] - min) > 1e-6)
            ticks.Insert(0, Math.Round(min, 10));

        if (Math.Abs(ticks[^1] - max) > 1e-6)
            ticks.Add(Math.Round(max, 10));

        return ticks
            .Distinct()
            .OrderBy(v => v)
            .ToList();
    }

    private static List<double> BuildNiceTicks(double min, double max, int targetCount)
    {
        if (max < min) (min, max) = (max, min);
        var step = NiceNumber((max - min) / Math.Max(1, targetCount - 1), true);
        var graphMin = Math.Floor(min / step) * step;
        var graphMax = Math.Ceiling(max / step) * step;
        var ticks = new List<double>();
        for (double v = graphMin; v <= graphMax + step * 0.5; v += step)
            ticks.Add(Math.Round(v, 10));
        return ticks;
    }

    private static double NiceNumber(double value, bool round)
    {
        if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            return 1;

        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10, exponent);
        double niceFraction;

        if (round)
        {
            if (fraction < 1.5) niceFraction = 1;
            else if (fraction < 3) niceFraction = 2;
            else if (fraction < 7) niceFraction = 5;
            else niceFraction = 10;
        }
        else
        {
            if (fraction <= 1) niceFraction = 1;
            else if (fraction <= 2) niceFraction = 2;
            else if (fraction <= 5) niceFraction = 5;
            else niceFraction = 10;
        }

        return niceFraction * Math.Pow(10, exponent);
    }

    private static string FormatTick(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 100 || Math.Abs(value - Math.Round(value)) < 1e-9)
            return value.ToString("0", CultureInfo.InvariantCulture);
        if (abs >= 10)
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static void DrawSeries(
        System.Drawing.Graphics g,
        System.Drawing.Pen pen,
        IReadOnlyList<double> xs,
        IReadOnlyList<double> ys,
        System.Drawing.Rectangle rect,
        double minX,
        double maxX,
        double minY,
        double maxY,
        bool drawMarkers = true)
    {
        if (xs.Count == 0 || ys.Count == 0) return;
        int n = Math.Min(xs.Count, ys.Count);
        var pts = new System.Drawing.PointF[n];
        for (int i = 0; i < n; i++)
            pts[i] = new System.Drawing.PointF(MapX(xs[i], minX, maxX, rect), MapY(ys[i], minY, maxY, rect));

        if (pts.Length == 1)
        {
            g.DrawEllipse(pen, pts[0].X - 2, pts[0].Y - 2, 4, 4);
            return;
        }

        g.DrawLines(pen, pts);
        if (!drawMarkers)
            return;

        using var fillBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        foreach (var p in pts)
        {
            g.FillEllipse(fillBrush, p.X - 2.5f, p.Y - 2.5f, 5, 5);
            g.DrawEllipse(pen, p.X - 2.5f, p.Y - 2.5f, 5, 5);
        }
    }

    private static float MapX(double x, double minX, double maxX, System.Drawing.Rectangle rect)
        => rect.Left + (float)((x - minX) / (maxX - minX) * rect.Width);

    private static float MapY(double y, double minY, double maxY, System.Drawing.Rectangle rect)
        => rect.Bottom - (float)((y - minY) / (maxY - minY) * rect.Height);

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string Esc(string s)
        => (s.Contains(',') || s.Contains('"')) ? '"' + s.Replace("\"", "\"\"") + '"' : s;

    private static int NaturalCompare(string a, string b)
    {
        var ra = Regex.Matches(a, @"\d+|\D+").Select(m => m.Value).ToArray();
        var rb = Regex.Matches(b, @"\d+|\D+").Select(m => m.Value).ToArray();
        int n = Math.Min(ra.Length, rb.Length);
        for (int i = 0; i < n; i++)
        {
            bool na = int.TryParse(ra[i], out int ia);
            bool nb = int.TryParse(rb[i], out int ib);
            int c = (na && nb)
                ? ia.CompareTo(ib)
                : string.Compare(ra[i], rb[i], StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }
        return ra.Length.CompareTo(rb.Length);
    }
}

