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

    public ObservableCollection<string> InspectionOptions { get; } = new() { "FRA", "DC SENSITIVITY", "SCAN", "SETTLING TIME", "SINE WAVE" };
    public ObservableCollection<string> FraModeOptions { get; } = new() { "OPEN LOOP", "PLANT" };
    public ObservableCollection<FraDataTypeDefinition> FraDataTypeOptions { get; } =
        new(FraDataTypeRegistry.Definitions);
    public ObservableCollection<FraPhaseOption> FraPhaseOptions { get; } = new()
    {
        new("Non-Inverted", false),
        new("Inverted", true),
    };

    public sealed record SettlingVendorOption(string Name, SettlingVendor Vendor)
    {
        public override string ToString() => Name;
    }

    public ObservableCollection<SettlingVendorOption> SettlingVendorOptions { get; } = new()
    {
        new("DW", SettlingVendor.DW),
        new("Robocube", SettlingVendor.Robocube),
        new("K-star", SettlingVendor.KStar),
        new("Anosys", SettlingVendor.Anosys),
    };

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

    private FraDataTypeDefinition? _selectedFraDataType;
    public FraDataTypeDefinition? SelectedFraDataType
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

    private bool _isEmpty = true;
    public bool IsEmpty { get => _isEmpty; set { _isEmpty = value; OnPropertyChanged(); } }

    public ObservableCollection<FraResultRow> FraResults { get; } = new();
    public ObservableCollection<DcSensitivityResultRow> DcResults { get; } = new();
    public ObservableCollection<ScanResultRow> ScanResults { get; } = new();
    public ObservableCollection<SettlingResultRow> SettlingResults { get; } = new();

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
        SaveSelectedSettlingImagesCommand = new RelayCommand(SaveSelectedSettlingImages, () => IsSettlingInspection && SelectedPlotFile is { IsSettling: true });

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

        PlotFiles.CollectionChanged += (_, __) => SaveSelectedFraOverlayCommand.RaiseCanExecuteChanged();

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
        PlotFiles.Clear();
        PlotFilesFiltered.Clear();
        SelectedPlotFile = null;
        SelectedFraRow = null;
        SelectedDcRow = null;
        SelectedScanRow = null;
        SelectedSettlingRow = null;
        RawPreview = null;
        ExportOutputLabel = "";
        RawMergedLabel = "";
        FraHoverFrequencyLabel = FraHoverInfoEmpty;
        OnPropertyChanged(nameof(SelectedOverlayCount));
        OnPropertyChanged(nameof(CanSaveSelectedFraOverlay));
        SaveSelectedFraOverlayCommand.RaiseCanExecuteChanged();
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
            default:
                ExportOutputLabel = "해당 검사 항목은 아직 구현되지 않았습니다.";
                break;
        }
    }

    private void ExportFraBatch()
    {
        var files = Directory.GetFiles(ImportedFolder, "*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "FRA");
            Directory.CreateDirectory(outDir);

            var dataType = SelectedFraDataType?.DataType ?? FraDataType.Auto;
            var loaded = new List<(string file, List<BodePoint> rawPts, List<BodePoint> processedPts, BodeMetricsResult m, double? g10)>();

            PlotFiles.Add(new PlotFileItem("ALL OVERLAY", "__ALL_OVERLAY__", Array.Empty<BodePoint>(), isAllOverlayItem: true));

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var rawPts = BodeCsvReader.Read(file, dataType);
                var processedPts = ApplyFraPhaseMode(rawPts);
                var m = BodeMetrics.Extract(processedPts, useCrossGuard: false);
                var g10 = FraHelpers.GainNearestAtHz(rawPts, 10.0);

                loaded.Add((file, rawPts, processedPts, m, g10));
                var plotItem = new PlotFileItem(Path.GetFileName(file), file, rawPts);
                plotItem.PropertyChanged += PlotItemOnPropertyChanged;
                PlotFiles.Add(plotItem);
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
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
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
                RawMergedLabel += Environment.NewLine + string.Join(Environment.NewLine, failures.Take(10));
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
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

        var files = Directory.GetFiles(ImportedFolder, SettlingCsvReader.FileSearchPattern(SelectedSettlingVendorOption.Vendor), SearchOption.TopDirectoryOnly);
        Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

        ResetResults();
        IsBusy = true;
        ProgressValue = 0;

        try
        {
            var outRoot = Path.Combine(ImportedFolder, "OUTPUT");
            Directory.CreateDirectory(outRoot);
            var outDir = Path.Combine(outRoot, "SETTLING TIME");
            Directory.CreateDirectory(outDir);

            var vendor = SelectedSettlingVendorOption.Vendor;
            double toleranceBand = ParseSpecNumber(SettlingSpec.ToleranceBand) ?? 0;
            double tailFraction = (ParseSpecNumber(SettlingSpec.TailWindowPercent) ?? 20) / 100.0;

            var failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                try
                {
                    var seriesList = SettlingCsvReader.Read(file, vendor);
                    string fileName = Path.GetFileName(file);
                    var grouped = GroupSettlingSeries(seriesList);
                    var groupMetrics = BuildSettlingGroupMetrics(grouped, toleranceBand, tailFraction, vendor);
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
                RawMergedLabel += Environment.NewLine + string.Join(Environment.NewLine, failures.Take(10));
        }
        catch (Exception ex)
        {
            ExportOutputLabel = "Error: " + ex.Message;
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
        SettlingVendor vendor)
    {
        double judgeStartTimeMs = SettlingCsvReader.JudgeStartTimeMs(vendor);
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

        var vendor = SelectedSettlingVendorOption?.Vendor ?? SettlingVendor.DW;
        var selectedFile = SelectedSettlingRow?.FileName;
        SettlingResults.Clear();

        foreach (var item in PlotFiles.Where(p => p.IsSettling))
        {
            var grouped = item.SettlingGroups.Select(g => (g.GroupLabel, Series: g.Series.ToList())).ToList();
            var groupMetrics = BuildSettlingGroupMetrics(grouped, toleranceBand, tailFraction, vendor);
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
            OnPropertyChanged(nameof(SelectedOverlayCount));
            OnPropertyChanged(nameof(CanSaveSelectedFraOverlay));
            SaveSelectedFraOverlayCommand.RaiseCanExecuteChanged();
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
    private void SaveSelectedSettlingImages()
    {
        if (!IsSettlingInspection) return;

        var file = SelectedPlotFile;
        if (file is null || !file.IsSettling)
        {
            System.Windows.MessageBox.Show(
                "파일 목록에서 이미지를 저장할 파일을 선택하세요.",
                "SETTLING TIME 이미지 저장",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var vendor = SelectedSettlingVendorOption?.Vendor ?? SettlingVendor.DW;
        double defaultToleranceBand = ParseSpecNumber(SettlingSpec.ToleranceBand) ?? 0;
        double tailFraction = (ParseSpecNumber(SettlingSpec.TailWindowPercent) ?? 20) / 100.0;

        var grouped = file.SettlingGroups.Select(g => (g.GroupLabel, Series: g.Series.ToList())).ToList();
        var groupMetrics = BuildSettlingGroupMetrics(grouped, defaultToleranceBand, tailFraction, vendor);

        string outDir = Path.Combine(
            HasImportedFolder ? ImportedFolder : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "OUTPUT", "SETTLING TIME", "Plots");
        Directory.CreateDirectory(outDir);

        string stem = SanitizeFileName(Path.GetFileNameWithoutExtension(file.FileName));
        foreach (var group in groupMetrics)
        {
            string path = Path.Combine(outDir, $"{stem}_{SanitizeFileName(group.GroupLabel)}.png");
            PlotImageExporter.SaveSettlingConditionPng(path, group.GroupLabel, group.Series, group.Result, group.Displacement, group.ToleranceBand);
        }

        System.Windows.MessageBox.Show(
            $"{groupMetrics.Count}장 저장 완료{Environment.NewLine}{outDir}",
            "SETTLING TIME 이미지 저장",
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

