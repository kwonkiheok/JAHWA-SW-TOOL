using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using MeasureHub.Services;

namespace MeasureHub.ViewModels;

/// <summary>
/// Control Tuning 작성 창.
/// 메인 창에서 Export 한 결과를 축(OIS X / OIS Y / AF)별로 가져와 빌드 블록 하나로 xlsx 에 추가한다.
///   - FRA: 축마다 시료를 그룹(최대 4개)에 지정 → 결과표 + 그룹별 Bode 오버레이
///   - DC Sensitivity: 축마다 대표 시료 1개 지정 → 대표 파형 + 누적 파형
/// 빌드 목록 탭(헤더 편집 / 삭제)은 두 검사가 같이 쓴다.
/// </summary>
public sealed class ControlTuningViewModel : ViewModelBase
{
    public const string InspectionFra = "FRA";
    public const string InspectionDc = "DC Sensitivity";

    private static readonly Dictionary<string, string[]> DefaultSheetNames = new()
    {
        [InspectionFra] = ["OL-FRA (Room Temp)", "OL-FRA (High Temp)", "Plant-FRA"],
        [InspectionDc] = ["DC Sens"],
    };

    private readonly MainViewModel _main;
    // 검사 항목별로 마지막에 쓴 시트를 기억한다.
    private readonly Dictionary<string, string> _sheetByInspection = new()
    {
        [InspectionFra] = "OL-FRA (Room Temp)",
        [InspectionDc] = "DC Sens",
    };

    public ControlTuningViewModel(MainViewModel main)
    {
        _main = main;
        for (int i = 0; i < FraHistoryWriter.MaxGroups; i++)
            GroupNames.Add(new GroupNameItem($"Cav{i + 1}"));
        foreach (var axis in HistoryBlocks.AxisOrder)
        {
            Axes.Add(new FraHistoryAxisViewModel(axis, this));
            DcAxes.Add(new DcHistoryAxisViewModel(axis, this));
        }
        SelectedAxis = Axes[^1];   // 보통 AF 부터 측정한다
        SelectedDcAxis = DcAxes[^1];

        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        AppendCommand = new RelayCommand(Append);
        RefreshBuildsCommand = new RelayCommand(RefreshBuilds);
        SelectFraCommand = new RelayCommand(() => Inspection = InspectionFra);
        SelectDcCommand = new RelayCommand(() => Inspection = InspectionDc);
        LoadSettings();
        RefreshSheetOptions();
    }

    // ---- 검사 항목 ----

    private string _inspection = InspectionFra;
    public string Inspection
    {
        get => _inspection;
        set
        {
            if (value == _inspection || !DefaultSheetNames.ContainsKey(value)) return;
            _inspection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFra));
            OnPropertyChanged(nameof(IsDc));
            if (SelectedTabIndex == 1) RefreshBuilds();
        }
    }

    public bool IsFra => Inspection == InspectionFra;
    public bool IsDc => Inspection == InspectionDc;
    public RelayCommand SelectFraCommand { get; }
    public RelayCommand SelectDcCommand { get; }

    // ---- 문서 / 시트 ----

    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(); } }

    // 시트 콤보는 검사 항목마다 따로 둔다(화면에서 IsFra / IsDc 로 바꿔 보인다).
    // 콤보 하나의 목록을 검사 전환 때마다 바꾸면 WPF ComboBox 가 선택을 잃어 시트 칸이 비어 보인다.

    /// <summary>현재 검사 항목의 시트.</summary>
    public string SheetName => _sheetByInspection[Inspection];

    public string FraSheetName { get => _sheetByInspection[InspectionFra]; set => SetSheet(InspectionFra, value); }
    public string DcSheetName { get => _sheetByInspection[InspectionDc]; set => SetSheet(InspectionDc, value); }

    private IReadOnlyList<string> _fraSheetOptions = [];
    public IReadOnlyList<string> FraSheetOptions { get => _fraSheetOptions; private set { _fraSheetOptions = value; OnPropertyChanged(); } }

    private IReadOnlyList<string> _dcSheetOptions = [];
    public IReadOnlyList<string> DcSheetOptions { get => _dcSheetOptions; private set { _dcSheetOptions = value; OnPropertyChanged(); } }

    private void SetSheet(string inspection, string? value)
    {
        if (string.IsNullOrEmpty(value) || value == _sheetByInspection[inspection]) return;   // 목록 갱신 중 빈 선택은 무시
        _sheetByInspection[inspection] = value;
        OnPropertyChanged(inspection == InspectionFra ? nameof(FraSheetName) : nameof(DcSheetName));
        if (inspection == Inspection && SelectedTabIndex == 1) RefreshBuilds();
    }

    /// <summary>검사별 기본 시트 + 문서에 이미 있는 시트. 선택한 시트는 목록에 항상 남긴다.</summary>
    private void RefreshSheetOptions()
    {
        var docSheets = new List<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath))
                docSheets.AddRange(HistoryBlocks.ListSheets(OutputPath));
        }
        catch { /* 문서를 못 읽으면 기본 목록만 */ }

        List<string> Build(string inspection)
        {
            var names = DefaultSheetNames[inspection].ToList();
            names.AddRange(docSheets.Where(n => !names.Contains(n)));
            if (!names.Contains(_sheetByInspection[inspection])) names.Add(_sheetByInspection[inspection]);
            return names;
        }
        FraSheetOptions = Build(InspectionFra);
        DcSheetOptions = Build(InspectionDc);
        OnPropertyChanged(nameof(FraSheetName));
        OnPropertyChanged(nameof(DcSheetName));
    }

    public RelayCommand BrowseOutputCommand { get; }

    private void BrowseOutput()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Control Tuning 문서 (새 파일 또는 기존 파일)",
            Filter = "Excel (*.xlsx)|*.xlsx",
            OverwritePrompt = false,   // 기존 파일이면 맨 아래에 추가한다
            FileName = string.IsNullOrWhiteSpace(OutputPath) ? "Control_Tuning_History.xlsx" : Path.GetFileName(OutputPath),
        };
        if (!string.IsNullOrWhiteSpace(OutputPath) && Directory.Exists(Path.GetDirectoryName(OutputPath)))
            dlg.InitialDirectory = Path.GetDirectoryName(OutputPath);
        if (dlg.ShowDialog() == true)
        {
            OutputPath = dlg.FileName;
            RefreshSheetOptions();
            if (SelectedTabIndex == 1) RefreshBuilds();
        }
    }

    // ---- 빌드 헤더 입력 ----

    private string _stage = "";
    public string Stage { get => _stage; set { _stage = value; OnPropertyChanged(); } }

    private string _maker = "";
    public string Maker { get => _maker; set { _maker = value; OnPropertyChanged(); } }

    private string _date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string Date { get => _date; set { _date = value; OnPropertyChanged(); } }

    private string _owner = "";
    public string Owner { get => _owner; set { _owner = value; OnPropertyChanged(); } }

    private string _reason = "";
    public string Reason { get => _reason; set { _reason = value; OnPropertyChanged(); } }

    private string _opinion = "";
    public string Opinion { get => _opinion; set { _opinion = value; OnPropertyChanged(); } }

    private HistoryHeader CurrentHeader => new(Stage, Maker, Date, Owner, Reason, Opinion);

    // ---- FRA ----

    public ObservableCollection<FraHistoryAxisViewModel> Axes { get; } = new();
    public ObservableCollection<GroupNameItem> GroupNames { get; } = new();

    private FraHistoryAxisViewModel? _selectedAxis;
    public FraHistoryAxisViewModel? SelectedAxis { get => _selectedAxis; set { _selectedAxis = value; OnPropertyChanged(); } }

    internal string GroupNameAt(int index)
        => index >= 0 && index < GroupNames.Count ? GroupNames[index].Name : "";

    internal IReadOnlyList<FraHistorySample> TakeCurrentFraResults()
    {
        var samples = _main.GetFraHistorySamples();
        if (samples.Count == 0)
            Info("가져올 FRA 결과가 없습니다.\n메인 창에서 FRA 검사로 Import → Export 를 먼저 실행하세요.");
        return samples;
    }

    // ---- DC Sensitivity ----

    public ObservableCollection<DcHistoryAxisViewModel> DcAxes { get; } = new();

    private DcHistoryAxisViewModel? _selectedDcAxis;
    public DcHistoryAxisViewModel? SelectedDcAxis { get => _selectedDcAxis; set { _selectedDcAxis = value; OnPropertyChanged(); } }

    internal IReadOnlyList<DcHistorySample> TakeCurrentDcResults()
    {
        var samples = _main.GetDcHistorySamples();
        if (samples.Count == 0)
            Info("가져올 DC Sensitivity 결과가 없습니다.\n메인 창에서 DC SENSITIVITY 검사로 Import → Export 를 먼저 실행하세요.");
        return samples;
    }

    // ---- 문서에 추가 ----

    public RelayCommand AppendCommand { get; }

    private void Append()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            Warn("저장할 문서 파일을 먼저 지정하세요.");
            return;
        }

        Action<string>? write = IsFra ? PrepareFra() : PrepareDc();
        if (write is null) return;

        bool existed = File.Exists(OutputPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "MeasureHub_ControlTuning", Guid.NewGuid().ToString("N"));
        bool ok;
        try { ok = RunOnDocument(() => write(tempDir)); }
        finally { try { Directory.Delete(tempDir, true); } catch { /* 임시 폴더 정리 실패는 무시 */ } }
        if (!ok) return;

        SaveSettings();
        RefreshSheetOptions();
        var answer = System.Windows.MessageBox.Show(
            $"{(existed ? $"'{SheetName}' 시트에 빌드를 추가했습니다." : "새 문서를 만들었습니다.")}\n{OutputPath}\n\n문서를 열까요?",
            "Control Tuning 작성", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);
        if (answer == System.Windows.MessageBoxResult.Yes)
        {
            try { Process.Start(new ProcessStartInfo(OutputPath) { UseShellExecute = true }); }
            catch (Exception ex) { Warn(ex.Message); }
        }
    }

    /// <summary>FRA 빌드를 검사하고, 문서에 쓰는 동작을 돌려준다 (문제가 있으면 null).</summary>
    private Action<string>? PrepareFra()
    {
        var axes = Axes.Select(a => a.BuildAxis()).Where(a => a.Groups.Count > 0).ToList();
        if (axes.Count == 0)
        {
            Warn("그룹이 지정된 시료가 없습니다.\n축 탭에서 FRA 결과를 가져온 뒤, 시료를 선택하고 그룹 버튼을 누르세요.");
            return null;
        }
        int unassigned = Axes.Sum(a => a.Items.Count(i => i.GroupIndex < 0));
        if (unassigned > 0 && !Confirm($"그룹이 지정되지 않은 시료 {unassigned}개는 문서에서 제외됩니다. 계속할까요?"))
            return null;

        var build = new FraHistoryBuild(CurrentHeader, axes);
        var spec = _main.GetFraHistorySpec();
        var (path, sheet) = (OutputPath, SheetName);
        return tempDir => FraHistoryWriter.AppendBuild(path, sheet, build, spec, tempDir);
    }

    /// <summary>DC 빌드를 검사하고, 문서에 쓰는 동작을 돌려준다 (문제가 있으면 null).</summary>
    private Action<string>? PrepareDc()
    {
        var axes = DcAxes.Select(a => a.BuildAxis()).OfType<DcHistoryAxis>().ToList();
        if (axes.Count == 0)
        {
            Warn("가져온 DC 결과가 없습니다.\n축 탭에서 '현재 DC 결과 가져오기'를 먼저 누르세요.");
            return null;
        }
        var build = new DcHistoryBuild(CurrentHeader, axes);
        var (path, sheet) = (OutputPath, SheetName);
        return tempDir => DcHistoryWriter.AppendBuild(path, sheet, build, tempDir);
    }

    // ---- 문서에 이미 들어간 빌드 목록 (헤더 편집 / 삭제) ----

    public RelayCommand RefreshBuildsCommand { get; }
    public ObservableCollection<HistoryBuildRowViewModel> Builds { get; } = new();

    private string _buildsStatus = "";
    public string BuildsStatus { get => _buildsStatus; set { _buildsStatus = value; OnPropertyChanged(); } }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); if (value == 1) RefreshBuilds(); }
    }

    private void RefreshBuilds()
    {
        Builds.Clear();
        if (string.IsNullOrWhiteSpace(OutputPath) || !File.Exists(OutputPath))
        {
            BuildsStatus = "문서 파일이 아직 없습니다. 위에서 파일을 지정하세요.";
            return;
        }
        try
        {
            var list = HistoryBlocks.ListBuilds(OutputPath, SheetName);
            for (int i = 0; i < list.Count; i++)
                Builds.Add(new HistoryBuildRowViewModel(i + 1, list[i], this));
            BuildsStatus = list.Count == 0
                ? $"'{SheetName}' 시트에 빌드가 없습니다."
                : $"'{SheetName}' 시트 빌드 {list.Count}개 · 칸을 더블클릭해 고친 뒤 [저장], 블록 전체를 지우려면 [삭제]";
        }
        catch (IOException ex) { BuildsStatus = "문서를 읽지 못했습니다. Excel 에서 열려 있으면 닫고 [새로 고침] 하세요. " + ex.Message; }
        catch (Exception ex) { BuildsStatus = "문서를 읽지 못했습니다: " + ex.Message; }
    }

    internal void SaveBuildHeader(HistoryBuildRowViewModel row)
    {
        if (!RunOnDocument(() => HistoryBlocks.UpdateHeader(OutputPath, SheetName, row.StartRow, row.ToHeader())))
            return;
        RefreshBuilds();
    }

    internal void DeleteBuild(HistoryBuildRowViewModel row)
    {
        if (!Confirm($"#{row.Index}  {row.Stage} / {row.Reason}\n\n이 빌드 블록(표와 그래프)을 문서에서 삭제할까요? 되돌릴 수 없습니다.", warning: true))
            return;
        if (!RunOnDocument(() => HistoryBlocks.DeleteBuild(OutputPath, SheetName, row.StartRow)))
            return;
        RefreshBuilds();
    }

    // ---- 공통 ----

    private static bool RunOnDocument(Action action)
    {
        try
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            action();
            return true;
        }
        catch (IOException ex)
        {
            Warn($"문서를 저장하지 못했습니다. Excel 에서 열려 있으면 닫고 다시 시도하세요.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            Warn($"작업 중 오류가 발생했습니다.\n\n{ex.Message}");
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }
        return false;
    }

    internal static void Info(string message)
        => System.Windows.MessageBox.Show(message, "Control Tuning 작성",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

    private static void Warn(string message)
        => System.Windows.MessageBox.Show(message, "Control Tuning 작성",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

    private static bool Confirm(string message, bool warning = false)
        => System.Windows.MessageBox.Show(message, "Control Tuning 작성", System.Windows.MessageBoxButton.YesNo,
               warning ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Question)
           == System.Windows.MessageBoxResult.Yes;

    // ---- 마지막 입력값 기억 (%AppData%\MeasureHub\control_tuning.json) ----

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeasureHub");
    private static string SettingsPath => Path.Combine(SettingsDir, "control_tuning.json");
    private static string LegacySettingsPath => Path.Combine(SettingsDir, "fra_history.json");   // FRA 만 있던 때의 설정

    private sealed class Settings
    {
        public string OutputPath { get; set; } = "";
        public string Inspection { get; set; } = "";
        public string SheetName { get; set; } = "";       // FRA 시트 (이전 설정 파일과 같은 이름)
        public string DcSheetName { get; set; } = "";
        public string Maker { get; set; } = "";
        public string Owner { get; set; } = "";
        public List<string> GroupNames { get; set; } = new();
    }

    private void LoadSettings()
    {
        try
        {
            var path = File.Exists(SettingsPath) ? SettingsPath : LegacySettingsPath;
            if (!File.Exists(path)) return;
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            if (s is null) return;
            OutputPath = s.OutputPath;
            if (!string.IsNullOrWhiteSpace(s.SheetName)) _sheetByInspection[InspectionFra] = s.SheetName;
            if (!string.IsNullOrWhiteSpace(s.DcSheetName)) _sheetByInspection[InspectionDc] = s.DcSheetName;
            if (DefaultSheetNames.ContainsKey(s.Inspection)) _inspection = s.Inspection;
            Maker = s.Maker;
            Owner = s.Owner;
            for (int i = 0; i < Math.Min(s.GroupNames.Count, GroupNames.Count); i++)
                GroupNames[i].Name = s.GroupNames[i];
        }
        catch { /* 설정 파일이 깨졌으면 기본값으로 시작 */ }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var s = new Settings
            {
                OutputPath = OutputPath, Inspection = Inspection,
                SheetName = _sheetByInspection[InspectionFra], DcSheetName = _sheetByInspection[InspectionDc],
                Maker = Maker, Owner = Owner,
                GroupNames = GroupNames.Select(g => g.Name).ToList(),
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 설정 저장 실패는 기능에 영향 없음 */ }
    }
}

/// <summary>CommandParameter 로 DataGrid.SelectedItems(IList) 를 받는 명령.</summary>
public sealed class SelectionCommand : System.Windows.Input.ICommand
{
    private readonly Action<System.Collections.IList?> _execute;
    public SelectionCommand(Action<System.Collections.IList?> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter as System.Collections.IList);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

/// <summary>문서의 빌드 블록 한 줄 (헤더 6칸 편집 + 저장 / 삭제).</summary>
public sealed class HistoryBuildRowViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;
    private readonly HistoryHeader _original;

    public HistoryBuildRowViewModel(int index, HistoryBlockInfo info, ControlTuningViewModel owner)
    {
        Index = index;
        StartRow = info.StartRow;
        RowRange = $"{info.StartRow}~{info.EndRow}행";
        _owner = owner;
        _original = info.Header;
        (_stage, _maker, _date, _ownerName, _reason, _opinion) =
            (info.Header.Stage, info.Header.Maker, info.Header.Date, info.Header.Owner, info.Header.Reason, info.Header.Opinion);
        SaveCommand = new RelayCommand(() => _owner.SaveBuildHeader(this), () => IsDirty);
        DeleteCommand = new RelayCommand(() => _owner.DeleteBuild(this));
    }

    public int Index { get; }
    public int StartRow { get; }
    public string RowRange { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }

    private string _stage, _maker, _date, _ownerName, _reason, _opinion;
    public string Stage { get => _stage; set { _stage = value; Changed(); } }
    public string Maker { get => _maker; set { _maker = value; Changed(); } }
    public string Date { get => _date; set { _date = value; Changed(); } }
    public string Owner { get => _ownerName; set { _ownerName = value; Changed(); } }
    public string Reason { get => _reason; set { _reason = value; Changed(); } }
    public string Opinion { get => _opinion; set { _opinion = value; Changed(); } }

    public bool IsDirty => ToHeader() != _original;

    internal HistoryHeader ToHeader() => new(Stage, Maker, Date, Owner, Reason, Opinion);

    private void Changed([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.RaiseCanExecuteChanged();
    }
}

/// <summary>그룹 이름 입력칸 (이름이 바뀌면 각 축 목록의 그룹 표시도 갱신된다).</summary>
public sealed class GroupNameItem : ViewModelBase
{
    public GroupNameItem(string name) => _name = name;

    private string _name;
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); NameChanged?.Invoke(); } }

    internal event Action? NameChanged;
}

/// <summary>축 탭 하나 (OIS X / OIS Y / AF).</summary>
public sealed class FraHistoryAxisViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public FraHistoryAxisViewModel(string axis, ControlTuningViewModel owner)
    {
        Axis = axis;
        _owner = owner;
        foreach (var g in owner.GroupNames)
            g.NameChanged += () => { foreach (var it in Items) it.RefreshGroupName(); RaiseSummary(); };

        ImportCommand = new RelayCommand(Import);
        ClearCommand = new RelayCommand(() => { Items.Clear(); RaiseSummary(); });
        AssignGroupCommands = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
            .Select(i => new SelectionCommand(sel => Assign(i, sel))).ToArray();
        UnassignCommand = new SelectionCommand(sel => Assign(-1, sel));
    }

    public string Axis { get; }
    public ObservableCollection<FraHistoryItemViewModel> Items { get; } = new();

    public RelayCommand ImportCommand { get; }
    public RelayCommand ClearCommand { get; }
    // 그룹 버튼은 표의 SelectedItems 를 CommandParameter 로 받는다.
    // (행마다 IsSelected 를 바인딩하던 방식은 Shift 범위 선택·스크롤 시 표시와 실제 선택이 어긋났다)
    public SelectionCommand[] AssignGroupCommands { get; }
    public SelectionCommand UnassignCommand { get; }

    public string Header => Items.Count == 0 ? Axis : $"{Axis} ({Items.Count})";
    public string Summary
    {
        get
        {
            if (Items.Count == 0) return "가져온 결과 없음";
            var parts = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
                .Select(i => (name: _owner.GroupNameAt(i), n: Items.Count(x => x.GroupIndex == i)))
                .Where(x => x.n > 0)
                .Select(x => $"{x.name} {x.n}");
            int none = Items.Count(x => x.GroupIndex < 0);
            return $"{Items.Count}개 · " + string.Join(", ", parts) + (none > 0 ? $" · 미지정 {none}" : "");
        }
    }

    private void Import()
    {
        var samples = _owner.TakeCurrentFraResults();
        if (samples.Count == 0) return;
        Items.Clear();
        foreach (var s in samples)
            Items.Add(new FraHistoryItemViewModel(s, _owner));
        RaiseSummary();
    }

    /// <summary>selection = DataGrid.SelectedItems (표에서 지금 선택된 행).</summary>
    private void Assign(int groupIndex, System.Collections.IList? selection)
    {
        var selected = selection?.OfType<FraHistoryItemViewModel>().ToList() ?? [];
        if (selected.Count == 0)
        {
            ControlTuningViewModel.Info("목록에서 시료를 먼저 선택하세요. (Ctrl/Shift 로 여러 개 선택, [전체 선택] 가능)");
            return;
        }
        foreach (var it in selected)
            it.GroupIndex = groupIndex;
        selection!.Clear();     // 지정이 끝난 행은 선택 해제 → 다음 그룹을 바로 고를 수 있게
        RaiseSummary();
    }

    internal void RaiseSummary()
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Summary));
    }

    internal FraHistoryAxis BuildAxis()
    {
        var groups = new List<(string, IReadOnlyList<FraHistorySample>)>();
        for (int i = 0; i < FraHistoryWriter.MaxGroups; i++)
        {
            var samples = Items.Where(x => x.GroupIndex == i).Select(x => x.Sample).ToList();
            if (samples.Count > 0)
                groups.Add((_owner.GroupNameAt(i), samples));
        }
        return new FraHistoryAxis(Axis, groups);
    }
}

/// <summary>축 탭의 시료 행.</summary>
public sealed class FraHistoryItemViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public FraHistoryItemViewModel(FraHistorySample sample, ControlTuningViewModel owner)
    {
        Sample = sample;
        _owner = owner;
    }

    public FraHistorySample Sample { get; }
    public string FileName => Sample.FileName;
    public string Judge => Sample.Judge;
    public string Gain10Hz => F(Sample.Gain10Hz);
    public string GM => F(Sample.GM);
    public string PM => F(Sample.PM);
    public string PM3 => F(Sample.PM3);

    private int _groupIndex = -1;
    public int GroupIndex
    {
        get => _groupIndex;
        set { _groupIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(GroupName)); }
    }

    public string GroupName => GroupIndex < 0 ? "(미지정)" : _owner.GroupNameAt(GroupIndex);

    internal void RefreshGroupName() => OnPropertyChanged(nameof(GroupName));

    private static string F(double? v) => v?.ToString("0.00", CultureInfo.InvariantCulture) ?? "N/A";
}

/// <summary>DC 축 탭 하나 (OIS X / OIS Y / AF). 가져온 시료 전체가 누적 파형, ★ 시료가 대표 파형.</summary>
public sealed class DcHistoryAxisViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public DcHistoryAxisViewModel(string axis, ControlTuningViewModel owner)
    {
        Axis = axis;
        _owner = owner;
        ImportCommand = new RelayCommand(Import);
        ClearCommand = new RelayCommand(() => { Items.Clear(); RaiseSummary(); });
        SetRepresentativeCommand = new RelayCommand(SetRepresentative);
    }

    public string Axis { get; }
    public ObservableCollection<DcHistoryItemViewModel> Items { get; } = new();

    public RelayCommand ImportCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SetRepresentativeCommand { get; }

    private DcHistoryItemViewModel? _selectedItem;
    public DcHistoryItemViewModel? SelectedItem { get => _selectedItem; set { _selectedItem = value; OnPropertyChanged(); } }

    public string Header => Items.Count == 0 ? Axis : $"{Axis} ({Items.Count})";
    public string Summary
    {
        get
        {
            if (Items.Count == 0) return "가져온 결과 없음";
            var rep = Items.FirstOrDefault(x => x.IsRepresentative);
            return $"{Items.Count}개 → 누적 파형 · 대표 파형: {rep?.FileName ?? "(없음)"}";
        }
    }

    private void Import()
    {
        var samples = _owner.TakeCurrentDcResults();
        if (samples.Count == 0) return;
        Items.Clear();
        foreach (var s in samples)
            Items.Add(new DcHistoryItemViewModel(s));
        Items[0].IsRepresentative = true;      // 기본 대표 = 첫 번째 파일
        RaiseSummary();
    }

    private void SetRepresentative()
    {
        if (SelectedItem is null)
        {
            ControlTuningViewModel.Info("목록에서 대표로 쓸 시료를 먼저 선택하세요.");
            return;
        }
        foreach (var it in Items) it.IsRepresentative = ReferenceEquals(it, SelectedItem);
        RaiseSummary();
    }

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>가져온 결과가 없으면 null.</summary>
    internal DcHistoryAxis? BuildAxis()
    {
        if (Items.Count == 0) return null;
        var rep = (Items.FirstOrDefault(x => x.IsRepresentative) ?? Items[0]).Sample;
        return new DcHistoryAxis(Axis, Items.Select(x => x.Sample).ToList(), rep);
    }
}

/// <summary>DC 축 탭의 시료 행.</summary>
public sealed class DcHistoryItemViewModel : ViewModelBase
{
    public DcHistoryItemViewModel(DcHistorySample sample) => Sample = sample;

    public DcHistorySample Sample { get; }
    public string FileName => Sample.FileName;
    public int UpCount => Sample.UpOutput.Count;
    public int DownCount => Sample.DownOutput.Count;
    public string OutputRange
    {
        get
        {
            var all = Sample.UpOutput.Concat(Sample.DownOutput).ToList();
            return all.Count == 0 ? "-" : $"{all.Min():0.#} ~ {all.Max():0.#}";
        }
    }

    private bool _isRepresentative;
    public bool IsRepresentative
    {
        get => _isRepresentative;
        set { _isRepresentative = value; OnPropertyChanged(); OnPropertyChanged(nameof(RepresentativeMark)); }
    }
    public string RepresentativeMark => IsRepresentative ? "★ 대표" : "";
}
