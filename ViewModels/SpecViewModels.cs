namespace JahwaInspector.Light.ViewModels;

using System.Collections.ObjectModel;
using JahwaInspector.Light.Services;

public sealed class EmptySpecViewModel : ViewModelBase { }

public sealed class FraSpecViewModel : ViewModelBase
{
    private string _gainMarginDb = "6";
    public string GainMarginDb { get => _gainMarginDb; set { _gainMarginDb = value; OnPropertyChanged(); } }

    private string _phaseMarginDeg = "45";
    public string PhaseMarginDeg { get => _phaseMarginDeg; set { _phaseMarginDeg = value; OnPropertyChanged(); } }

    private string _phaseMarginMinus3dBDeg = "45";
    public string PhaseMarginMinus3dBDeg { get => _phaseMarginMinus3dBDeg; set { _phaseMarginMinus3dBDeg = value; OnPropertyChanged(); } }

    private bool _savePlotImage = false;
    /// <summary>
    /// Export 시 Bode Plot을 PNG로 추가 저장할지 여부
    /// </summary>
    public bool SavePlotImage { get => _savePlotImage; set { _savePlotImage = value; OnPropertyChanged(); } }

    private string _lastGm = "-";
    public string LastGm { get => _lastGm; set { _lastGm = value; OnPropertyChanged(); } }

    private string _lastPm = "-";
    public string LastPm { get => _lastPm; set { _lastPm = value; OnPropertyChanged(); } }

    private string _lastGmFreq = "-";
    public string LastGmFreq { get => _lastGmFreq; set { _lastGmFreq = value; OnPropertyChanged(); } }

    private string _lastPmFreq = "-";
    public string LastPmFreq { get => _lastPmFreq; set { _lastPmFreq = value; OnPropertyChanged(); } }

    private string _last10HzGain = "-";
    public string Last10HzGain { get => _last10HzGain; set { _last10HzGain = value; OnPropertyChanged(); } }

    private string _lastPm3 = "-";
    public string LastPm3 { get => _lastPm3; set { _lastPm3 = value; OnPropertyChanged(); } }

    private string _lastPm3Freq = "-";
    public string LastPm3Freq { get => _lastPm3Freq; set { _lastPm3Freq = value; OnPropertyChanged(); } }

    private string _sourceFile = "";
    public string SourceFile { get => _sourceFile; set { _sourceFile = value; OnPropertyChanged(); } }
    public double? MinGM { get; set; }      // 예: 10
    public double? MinPM { get; set; }      // 예: 45
    public double? Min10HzGain { get; set; } // 예: 20
    public double? MinPM3 { get; set; }     // 필요하면
    public bool EnableJudgement { get; set; } = true;
}


public sealed class DcSensitivitySpecViewModel : ViewModelBase
{
    public IReadOnlyList<string> DataTypeOptions { get; } = DcSensitivityDataTypeRegistry.Names;

    private string _selectedDataType = "AKM";
    public string SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            _selectedDataType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredInputHeader));
            OnPropertyChanged(nameof(ConfiguredOutputHeader));
            OnPropertyChanged(nameof(ConfiguredHeaderDescription));
        }
    }

    public string ConfiguredInputHeader => DcSensitivityDataTypeRegistry.Get(SelectedDataType).InputHeaderDisplay;

    public string ConfiguredOutputHeader => DcSensitivityDataTypeRegistry.Get(SelectedDataType).OutputHeaderDisplay;

    public string ConfiguredHeaderDescription => DcSensitivityDataTypeRegistry.Get(SelectedDataType).HeaderRuleDescription;

    private string _inputCandidatesText = "input, position, position code [dec], position code, target, code";
    public string InputCandidatesText { get => _inputCandidatesText; set { _inputCandidatesText = value; OnPropertyChanged(); } }

    private string _outputCandidatesText = "output, laser, hall, value";
    public string OutputCandidatesText { get => _outputCandidatesText; set { _outputCandidatesText = value; OnPropertyChanged(); } }

    private int _headerScanRows = 30;
    public int HeaderScanRows { get => _headerScanRows; set { _headerScanRows = value; OnPropertyChanged(); } }

    private bool _savePlotImage = true;
    public bool SavePlotImage { get => _savePlotImage; set { _savePlotImage = value; OnPropertyChanged(); } }

    private bool _saveCombinedCsv = true;
    public bool SaveCombinedCsv { get => _saveCombinedCsv; set { _saveCombinedCsv = value; OnPropertyChanged(); } }

    private bool _saveSummaryExcel = true;
    public bool SaveSummaryExcel { get => _saveSummaryExcel; set { _saveSummaryExcel = value; OnPropertyChanged(); } }

    private string _lastSummary = "-";
    public string LastSummary { get => _lastSummary; set { _lastSummary = value; OnPropertyChanged(); } }
}

public sealed class ScanSpecViewModel : ViewModelBase
{
    public IReadOnlyList<string> DataTypeOptions { get; } = ScanDataTypeRegistry.Names;

    private string _selectedDataType = "AKM";
    public string SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            _selectedDataType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredInputHeader));
            OnPropertyChanged(nameof(ConfiguredOutputHeader));
            OnPropertyChanged(nameof(ConfiguredHeaderDescription));
            OnPropertyChanged(nameof(UsesAutomaticRange));
            OnPropertyChanged(nameof(AutoRangeDescription));
        }
    }

    private string _inputCandidatesText = "position code [dec]";
    public string InputCandidatesText
    {
        get => _inputCandidatesText;
        set
        {
            _inputCandidatesText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredInputHeader));
        }
    }

    private string _outputCandidatesText = "output";
    public string OutputCandidatesText
    {
        get => _outputCandidatesText;
        set
        {
            _outputCandidatesText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredOutputHeader));
        }
    }

    public string ConfiguredInputHeader => ScanDataTypeRegistry.Get(SelectedDataType).InputHeaderDisplay;

    public string ConfiguredOutputHeader => ScanDataTypeRegistry.Get(SelectedDataType).OutputHeaderDisplay;

    public string ConfiguredHeaderDescription => ScanDataTypeRegistry.Get(SelectedDataType).HeaderRuleDescription;

    public bool UsesAutomaticRange => ScanDataTypeRegistry.Get(SelectedDataType).UsesAutomaticRange;

    public string AutoRangeDescription => ScanDataTypeRegistry.Get(SelectedDataType).RangeRuleDescription;

    private string _rangesText = "0-4080";
    public string RangesText
    {
        get => _rangesText;
        set
        {
            _rangesText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoRangeDescription));
        }
    }

    private string _specHys1 = "+-10";
    public string SpecHys1 { get => _specHys1; set { _specHys1 = value; OnPropertyChanged(); } }

    private string _specLi1 = "+-10";
    public string SpecLi1 { get => _specLi1; set { _specLi1 = value; OnPropertyChanged(); } }

    private string _specHys2 = "+-10";
    public string SpecHys2 { get => _specHys2; set { _specHys2 = value; OnPropertyChanged(); } }

    private string _specLi2 = "+-10";
    public string SpecLi2 { get => _specLi2; set { _specLi2 = value; OnPropertyChanged(); } }

    private bool _savePlotImage = true;
    public bool SavePlotImage { get => _savePlotImage; set { _savePlotImage = value; OnPropertyChanged(); } }

    private bool _saveSummaryExcel = true;
    public bool SaveSummaryExcel { get => _saveSummaryExcel; set { _saveSummaryExcel = value; OnPropertyChanged(); } }

    private string _lastRange1 = "-";
    public string LastRange1 { get => _lastRange1; set { _lastRange1 = value; OnPropertyChanged(); } }

    private string _lastHys1 = "-";
    public string LastHys1 { get => _lastHys1; set { _lastHys1 = value; OnPropertyChanged(); } }

    private string _lastLi1 = "-";
    public string LastLi1 { get => _lastLi1; set { _lastLi1 = value; OnPropertyChanged(); } }

    private string _lastRange2 = "-";
    public string LastRange2 { get => _lastRange2; set { _lastRange2 = value; OnPropertyChanged(); } }

    private string _lastHys2 = "-";
    public string LastHys2 { get => _lastHys2; set { _lastHys2 = value; OnPropertyChanged(); } }

    private string _lastLi2 = "-";
    public string LastLi2 { get => _lastLi2; set { _lastLi2 = value; OnPropertyChanged(); } }

    private string _lastJudge = "-";
    public string LastJudge { get => _lastJudge; set { _lastJudge = value; OnPropertyChanged(); } }

    private string _lastReason = "-";
    public string LastReason { get => _lastReason; set { _lastReason = value; OnPropertyChanged(); } }

    private string _sourceFile = "";
    public string SourceFile { get => _sourceFile; set { _sourceFile = value; OnPropertyChanged(); } }

    private string _lastSummary = "-";
    public string LastSummary { get => _lastSummary; set { _lastSummary = value; OnPropertyChanged(); } }
}

public sealed class TiltSpecViewModel : ViewModelBase
{
    public IReadOnlyList<string> DataTypeOptions { get; } = TiltDataTypeRegistry.Names;

    private string _selectedDataType = "robocube";
    public string SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            _selectedDataType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredInputHeader));
            OnPropertyChanged(nameof(ConfiguredOutputHeader));
            OnPropertyChanged(nameof(ConfiguredHeaderDescription));
        }
    }

    public string ConfiguredInputHeader => TiltDataTypeRegistry.Get(SelectedDataType).InputHeaderDisplay;

    public string ConfiguredOutputHeader => TiltDataTypeRegistry.Get(SelectedDataType).OutputHeaderDisplay;

    public string ConfiguredHeaderDescription => TiltDataTypeRegistry.Get(SelectedDataType).HeaderRuleDescription;

    private string _specTilt = "3";
    public string SpecTilt { get => _specTilt; set { _specTilt = value; OnPropertyChanged(); } }

    private bool _savePlotImage = true;
    public bool SavePlotImage { get => _savePlotImage; set { _savePlotImage = value; OnPropertyChanged(); } }

    private bool _saveSummaryExcel = true;
    public bool SaveSummaryExcel { get => _saveSummaryExcel; set { _saveSummaryExcel = value; OnPropertyChanged(); } }

    private string _lastPeakUp = "-";
    public string LastPeakUp { get => _lastPeakUp; set { _lastPeakUp = value; OnPropertyChanged(); } }

    private string _lastPeakDown = "-";
    public string LastPeakDown { get => _lastPeakDown; set { _lastPeakDown = value; OnPropertyChanged(); } }

    private string _lastJudge = "-";
    public string LastJudge { get => _lastJudge; set { _lastJudge = value; OnPropertyChanged(); } }

    private string _lastReason = "-";
    public string LastReason { get => _lastReason; set { _lastReason = value; OnPropertyChanged(); } }

    private string _sourceFile = "";
    public string SourceFile { get => _sourceFile; set { _sourceFile = value; OnPropertyChanged(); } }

    private string _lastSummary = "-";
    public string LastSummary { get => _lastSummary; set { _lastSummary = value; OnPropertyChanged(); } }
}

public sealed class SinewaveSpecViewModel : ViewModelBase
{
    public IReadOnlyList<string> DataTypeOptions { get; } = SinewaveDataTypeRegistry.Names;

    private string _selectedDataType = "AKM";
    public string SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            _selectedDataType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredInputHeader));
            OnPropertyChanged(nameof(ConfiguredOutputHeader));
            OnPropertyChanged(nameof(ConfiguredHeaderDescription));
        }
    }

    public string ConfiguredInputHeader => SinewaveDataTypeRegistry.Get(SelectedDataType).InputHeaderDisplay;

    public string ConfiguredOutputHeader => SinewaveDataTypeRegistry.Get(SelectedDataType).OutputHeaderDisplay;

    public string ConfiguredHeaderDescription => SinewaveDataTypeRegistry.Get(SelectedDataType).HeaderRuleDescription;

    private string _specMaxErr = "50";
    public string SpecMaxErr { get => _specMaxErr; set { _specMaxErr = value; OnPropertyChanged(); } }

    private bool _savePlotImage = true;
    public bool SavePlotImage { get => _savePlotImage; set { _savePlotImage = value; OnPropertyChanged(); } }

    private bool _saveSummaryExcel = true;
    public bool SaveSummaryExcel { get => _saveSummaryExcel; set { _saveSummaryExcel = value; OnPropertyChanged(); } }

    private string _lastMaxErrX = "-";
    public string LastMaxErrX { get => _lastMaxErrX; set { _lastMaxErrX = value; OnPropertyChanged(); } }

    private string _lastMaxErrY = "-";
    public string LastMaxErrY { get => _lastMaxErrY; set { _lastMaxErrY = value; OnPropertyChanged(); } }

    private string _lastJudge = "-";
    public string LastJudge { get => _lastJudge; set { _lastJudge = value; OnPropertyChanged(); } }

    private string _lastReason = "-";
    public string LastReason { get => _lastReason; set { _lastReason = value; OnPropertyChanged(); } }

    private string _sourceFile = "";
    public string SourceFile { get => _sourceFile; set { _sourceFile = value; OnPropertyChanged(); } }

    private string _lastSummary = "-";
    public string LastSummary { get => _lastSummary; set { _lastSummary = value; OnPropertyChanged(); } }
}

public sealed class CircleTestSpecViewModel : ViewModelBase
{
    public IReadOnlyList<string> DataTypeOptions { get; } = CircleTestDataTypeRegistry.Names;

    private string _selectedDataType = "AKM";
    public string SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            _selectedDataType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredInputHeader));
            OnPropertyChanged(nameof(ConfiguredOutputHeader));
            OnPropertyChanged(nameof(ConfiguredHeaderDescription));
        }
    }

    public string ConfiguredInputHeader => CircleTestDataTypeRegistry.Get(SelectedDataType).InputHeaderDisplay;

    public string ConfiguredOutputHeader => CircleTestDataTypeRegistry.Get(SelectedDataType).OutputHeaderDisplay;

    public string ConfiguredHeaderDescription => CircleTestDataTypeRegistry.Get(SelectedDataType).HeaderRuleDescription;

    private string _specMaxErr = "150";
    public string SpecMaxErr { get => _specMaxErr; set { _specMaxErr = value; OnPropertyChanged(); } }

    private bool _savePlotImage = true;
    public bool SavePlotImage { get => _savePlotImage; set { _savePlotImage = value; OnPropertyChanged(); } }

    private bool _saveSummaryExcel = true;
    public bool SaveSummaryExcel { get => _saveSummaryExcel; set { _saveSummaryExcel = value; OnPropertyChanged(); } }

    private string _lastMaxErrX = "-";
    public string LastMaxErrX { get => _lastMaxErrX; set { _lastMaxErrX = value; OnPropertyChanged(); } }

    private string _lastMaxErrY = "-";
    public string LastMaxErrY { get => _lastMaxErrY; set { _lastMaxErrY = value; OnPropertyChanged(); } }

    private string _lastJudge = "-";
    public string LastJudge { get => _lastJudge; set { _lastJudge = value; OnPropertyChanged(); } }

    private string _lastReason = "-";
    public string LastReason { get => _lastReason; set { _lastReason = value; OnPropertyChanged(); } }

    private string _sourceFile = "";
    public string SourceFile { get => _sourceFile; set { _sourceFile = value; OnPropertyChanged(); } }

    private string _lastSummary = "-";
    public string LastSummary { get => _lastSummary; set { _lastSummary = value; OnPropertyChanged(); } }
}

public sealed class SquareTestSpecViewModel : ViewModelBase
{
    public IReadOnlyList<string> DataTypeOptions { get; } = SquareTestDataTypeRegistry.Names;

    private string _selectedDataType = "AKM";
    public string SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            _selectedDataType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfiguredInputHeader));
            OnPropertyChanged(nameof(ConfiguredOutputHeader));
            OnPropertyChanged(nameof(ConfiguredHeaderDescription));
        }
    }

    public string ConfiguredInputHeader => SquareTestDataTypeRegistry.Get(SelectedDataType).InputHeaderDisplay;

    public string ConfiguredOutputHeader => SquareTestDataTypeRegistry.Get(SelectedDataType).OutputHeaderDisplay;

    public string ConfiguredHeaderDescription => SquareTestDataTypeRegistry.Get(SelectedDataType).HeaderRuleDescription;

    private string _specToleranceBandPercent = "5";
    public string SpecToleranceBandPercent { get => _specToleranceBandPercent; set { _specToleranceBandPercent = value; OnPropertyChanged(); } }

    private string _specMaxSettlingTimeMs = "30";
    public string SpecMaxSettlingTimeMs { get => _specMaxSettlingTimeMs; set { _specMaxSettlingTimeMs = value; OnPropertyChanged(); } }

    private string _specMaxOvershoot = "170";
    public string SpecMaxOvershoot { get => _specMaxOvershoot; set { _specMaxOvershoot = value; OnPropertyChanged(); } }

    private bool _savePlotImage = true;
    public bool SavePlotImage { get => _savePlotImage; set { _savePlotImage = value; OnPropertyChanged(); } }

    private bool _saveSummaryExcel = true;
    public bool SaveSummaryExcel { get => _saveSummaryExcel; set { _saveSummaryExcel = value; OnPropertyChanged(); } }

    private string _lastSettling = "-";
    public string LastSettling { get => _lastSettling; set { _lastSettling = value; OnPropertyChanged(); } }

    private string _lastOvershoot = "-";
    public string LastOvershoot { get => _lastOvershoot; set { _lastOvershoot = value; OnPropertyChanged(); } }

    private string _lastSettlingUp = "-";
    public string LastSettlingUp { get => _lastSettlingUp; set { _lastSettlingUp = value; OnPropertyChanged(); } }

    private string _lastOvershootUp = "-";
    public string LastOvershootUp { get => _lastOvershootUp; set { _lastOvershootUp = value; OnPropertyChanged(); } }

    private string _lastSettlingDown = "-";
    public string LastSettlingDown { get => _lastSettlingDown; set { _lastSettlingDown = value; OnPropertyChanged(); } }

    private string _lastOvershootDown = "-";
    public string LastOvershootDown { get => _lastOvershootDown; set { _lastOvershootDown = value; OnPropertyChanged(); } }

    private string _lastJudge = "-";
    public string LastJudge { get => _lastJudge; set { _lastJudge = value; OnPropertyChanged(); } }

    private string _lastReason = "-";
    public string LastReason { get => _lastReason; set { _lastReason = value; OnPropertyChanged(); } }

    private string _sourceFile = "";
    public string SourceFile { get => _sourceFile; set { _sourceFile = value; OnPropertyChanged(); } }

    private string _lastSummary = "-";
    public string LastSummary { get => _lastSummary; set { _lastSummary = value; OnPropertyChanged(); } }
}

public sealed class SettlingSpecViewModel : ViewModelBase
{
    private string _toleranceBand = "100";
    public string ToleranceBand { get => _toleranceBand; set { _toleranceBand = value; OnPropertyChanged(); } }

    public ObservableCollection<SettlingConditionTolerance> ConditionTolerances { get; } = new();

    private string _tailWindowPercent = "20";
    public string TailWindowPercent { get => _tailWindowPercent; set { _tailWindowPercent = value; OnPropertyChanged(); } }

    private string _maxSettlingTimeMs = "50";
    public string MaxSettlingTimeMs { get => _maxSettlingTimeMs; set { _maxSettlingTimeMs = value; OnPropertyChanged(); } }

    private string _lastTarget = "-";
    public string LastTarget { get => _lastTarget; set { _lastTarget = value; OnPropertyChanged(); } }

    private string _lastSettlingTimeMs = "-";
    public string LastSettlingTimeMs { get => _lastSettlingTimeMs; set { _lastSettlingTimeMs = value; OnPropertyChanged(); } }

    private string _lastOvershoot = "-";
    public string LastOvershoot { get => _lastOvershoot; set { _lastOvershoot = value; OnPropertyChanged(); } }

    private string _lastJudge = "-";
    public string LastJudge { get => _lastJudge; set { _lastJudge = value; OnPropertyChanged(); } }

    private string _lastReason = "-";
    public string LastReason { get => _lastReason; set { _lastReason = value; OnPropertyChanged(); } }

    private string _sourceFile = "";
    public string SourceFile { get => _sourceFile; set { _sourceFile = value; OnPropertyChanged(); } }

    private string _lastSummary = "-";
    public string LastSummary { get => _lastSummary; set { _lastSummary = value; OnPropertyChanged(); } }
}
