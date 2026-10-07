using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using MeasureHub.Services;

namespace MeasureHub.ViewModels;

/// <summary>
/// FRA History 문서 창.
/// 메인 창에서 FRA Export 한 결과를 축(OIS X / OIS Y / AF)별로 가져와 그룹(최대 4개)을 지정하고,
/// 빌드 블록 하나로 xlsx 에 추가한다.
/// </summary>
public sealed class FraHistoryViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public FraHistoryViewModel(MainViewModel main)
    {
        _main = main;
        for (int i = 0; i < FraHistoryWriter.MaxGroups; i++)
            GroupNames.Add(new GroupNameItem($"Cav{i + 1}"));
        foreach (var axis in FraHistoryWriter.AxisOrder)
            Axes.Add(new FraHistoryAxisViewModel(axis, this));
        SelectedAxis = Axes[^1];   // 보통 AF 부터 측정한다

        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        AppendCommand = new RelayCommand(Append);
        RefreshBuildsCommand = new RelayCommand(RefreshBuilds);
        LoadSettings();
        RefreshSheetOptions();
    }

    public RelayCommand RefreshBuildsCommand { get; }

    // ---- 문서에 이미 들어간 빌드 목록 (헤더 편집 / 삭제) ----

    public ObservableCollection<FraHistoryBuildRowViewModel> Builds { get; } = new();

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
            var list = FraHistoryWriter.ListBuilds(OutputPath, SheetName);
            for (int i = 0; i < list.Count; i++)
                Builds.Add(new FraHistoryBuildRowViewModel(i + 1, list[i], this));
            BuildsStatus = list.Count == 0
                ? $"'{SheetName}' 시트에 빌드가 없습니다."
                : $"'{SheetName}' 시트 빌드 {list.Count}개 · 칸을 더블클릭해 고친 뒤 [저장], 블록 전체를 지우려면 [삭제]";
        }
        catch (IOException ex) { BuildsStatus = "문서를 읽지 못했습니다. Excel 에서 열려 있으면 닫고 [새로 고침] 하세요. " + ex.Message; }
        catch (Exception ex) { BuildsStatus = "문서를 읽지 못했습니다: " + ex.Message; }
    }

    internal void SaveBuildHeader(FraHistoryBuildRowViewModel row)
    {
        if (!RunOnDocument(() => FraHistoryWriter.UpdateHeader(OutputPath, SheetName, row.StartRow, row.ToHeader())))
            return;
        RefreshBuilds();
    }

    internal void DeleteBuild(FraHistoryBuildRowViewModel row)
    {
        var answer = System.Windows.MessageBox.Show(
            $"#{row.Index}  {row.Stage} / {row.Reason}\n\n이 빌드 블록(표와 그래프)을 문서에서 삭제할까요? 되돌릴 수 없습니다.",
            "Control Tuning 작성", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (answer != System.Windows.MessageBoxResult.Yes) return;
        if (!RunOnDocument(() => FraHistoryWriter.DeleteBuild(OutputPath, SheetName, row.StartRow)))
            return;
        RefreshBuilds();
    }

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

    public ObservableCollection<FraHistoryAxisViewModel> Axes { get; } = new();
    public ObservableCollection<GroupNameItem> GroupNames { get; } = new();
    private static readonly string[] DefaultSheetNames = ["OL-FRA (Room Temp)", "OL-FRA (High Temp)", "Plant-FRA"];

    /// <summary>기본 시트 3개 + 문서에 이미 있는 시트.</summary>
    public ObservableCollection<string> SheetNameOptions { get; } = new(DefaultSheetNames);

    private void RefreshSheetOptions()
    {
        var names = DefaultSheetNames.ToList();
        try
        {
            if (!string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath))
                names.AddRange(FraHistoryWriter.ListSheets(OutputPath).Where(n => !names.Contains(n)));
        }
        catch { /* 문서를 못 읽으면 기본 목록만 */ }
        if (!names.Contains(SheetName)) names.Add(SheetName);

        var keep = SheetName;
        SheetNameOptions.Clear();
        foreach (var n in names) SheetNameOptions.Add(n);
        SheetName = keep;
    }

    private FraHistoryAxisViewModel? _selectedAxis;
    public FraHistoryAxisViewModel? SelectedAxis { get => _selectedAxis; set { _selectedAxis = value; OnPropertyChanged(); } }

    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(); } }

    private string _sheetName = "OL-FRA (Room Temp)";
    public string SheetName
    {
        get => _sheetName;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _sheetName) return;   // 목록 갱신 중 빈 선택은 무시
            _sheetName = value;
            OnPropertyChanged();
            if (SelectedTabIndex == 1) RefreshBuilds();
        }
    }

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

    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand AppendCommand { get; }

    internal string GroupNameAt(int index)
        => index >= 0 && index < GroupNames.Count ? GroupNames[index].Name : "";

    internal IReadOnlyList<FraHistorySample> TakeCurrentFraResults()
    {
        var samples = _main.GetFraHistorySamples();
        if (samples.Count == 0)
            System.Windows.MessageBox.Show(
                "가져올 FRA 결과가 없습니다.\n메인 창에서 FRA 검사로 Import → Export 를 먼저 실행하세요.",
                "Control Tuning 작성", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        return samples;
    }

    private void BrowseOutput()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "FRA History 문서 (새 파일 또는 기존 파일)",
            Filter = "Excel (*.xlsx)|*.xlsx",
            OverwritePrompt = false,   // 기존 파일이면 맨 아래에 추가한다
            FileName = string.IsNullOrWhiteSpace(OutputPath) ? "FRA_History.xlsx" : Path.GetFileName(OutputPath),
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

    private void Append()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            Warn("저장할 문서 파일을 먼저 지정하세요.");
            return;
        }

        var axes = Axes.Select(a => a.BuildAxis()).Where(a => a.Groups.Count > 0).ToList();
        if (axes.Count == 0)
        {
            Warn("그룹이 지정된 시료가 없습니다.\n축 탭에서 FRA 결과를 가져온 뒤, 시료를 선택하고 그룹 버튼을 누르세요.");
            return;
        }

        int unassigned = Axes.Sum(a => a.Items.Count(i => i.GroupIndex < 0));
        if (unassigned > 0 &&
            System.Windows.MessageBox.Show($"그룹이 지정되지 않은 시료 {unassigned}개는 문서에서 제외됩니다. 계속할까요?",
                "Control Tuning 작성", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
            != System.Windows.MessageBoxResult.Yes)
            return;

        bool existed = File.Exists(OutputPath);
        var build = new FraHistoryBuild(Stage, Maker, Date, Owner, Reason, Opinion, axes);
        var tempDir = Path.Combine(Path.GetTempPath(), "MeasureHub_FraHistory", Guid.NewGuid().ToString("N"));
        try
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            FraHistoryWriter.AppendBuild(OutputPath, SheetName, build, _main.GetFraHistorySpec(), tempDir);
        }
        catch (IOException ex)
        {
            Warn($"문서를 저장하지 못했습니다. Excel 에서 열려 있으면 닫고 다시 시도하세요.\n\n{ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            Warn($"문서 생성 중 오류가 발생했습니다.\n\n{ex.Message}");
            return;
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
            try { Directory.Delete(tempDir, true); } catch { /* 임시 폴더 정리 실패는 무시 */ }
        }

        SaveSettings();
        RefreshSheetOptions();
        var answer = System.Windows.MessageBox.Show(
            $"{(existed ? "기존 문서에 빌드를 추가했습니다." : "새 문서를 만들었습니다.")}\n{OutputPath}\n\n문서를 열까요?",
            "Control Tuning 작성", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);
        if (answer == System.Windows.MessageBoxResult.Yes)
        {
            try { Process.Start(new ProcessStartInfo(OutputPath) { UseShellExecute = true }); }
            catch (Exception ex) { Warn(ex.Message); }
        }
    }

    private static void Warn(string message)
        => System.Windows.MessageBox.Show(message, "Control Tuning 작성",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

    // ---- 마지막 입력값 기억 (%AppData%\MeasureHub\fra_history.json) ----

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeasureHub", "fra_history.json");

    private sealed class Settings
    {
        public string OutputPath { get; set; } = "";
        public string SheetName { get; set; } = "";
        public string Maker { get; set; } = "";
        public string Owner { get; set; } = "";
        public List<string> GroupNames { get; set; } = new();
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
            if (s is null) return;
            OutputPath = s.OutputPath;
            if (!string.IsNullOrWhiteSpace(s.SheetName)) SheetName = s.SheetName;
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
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var s = new Settings
            {
                OutputPath = OutputPath, SheetName = SheetName, Maker = Maker, Owner = Owner,
                GroupNames = GroupNames.Select(g => g.Name).ToList(),
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 설정 저장 실패는 기능에 영향 없음 */ }
    }
}

/// <summary>문서의 빌드 블록 한 줄 (헤더 6칸 편집 + 저장 / 삭제).</summary>
public sealed class FraHistoryBuildRowViewModel : ViewModelBase
{
    private readonly FraHistoryViewModel _owner;
    private readonly FraHistoryHeader _original;

    public FraHistoryBuildRowViewModel(int index, FraHistoryBlockInfo info, FraHistoryViewModel owner)
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

    internal FraHistoryHeader ToHeader() => new(Stage, Maker, Date, Owner, Reason, Opinion);

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
    private readonly FraHistoryViewModel _owner;

    public FraHistoryAxisViewModel(string axis, FraHistoryViewModel owner)
    {
        Axis = axis;
        _owner = owner;
        foreach (var g in owner.GroupNames)
            g.NameChanged += () => { foreach (var it in Items) it.RefreshGroupName(); RaiseSummary(); };

        ImportCommand = new RelayCommand(Import);
        ClearCommand = new RelayCommand(() => { Items.Clear(); RaiseSummary(); });
        AssignGroupCommands = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
            .Select(i => new RelayCommand(() => Assign(i))).ToArray();
        UnassignCommand = new RelayCommand(() => Assign(-1));
        SelectAllCommand = new RelayCommand(() => { foreach (var it in Items) it.IsSelected = true; });
    }

    public string Axis { get; }
    public ObservableCollection<FraHistoryItemViewModel> Items { get; } = new();

    public RelayCommand ImportCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand[] AssignGroupCommands { get; }
    public RelayCommand UnassignCommand { get; }
    public RelayCommand SelectAllCommand { get; }

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

    private void Assign(int groupIndex)
    {
        var selected = Items.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0)
        {
            System.Windows.MessageBox.Show("목록에서 시료를 먼저 선택하세요. (Ctrl/Shift 로 여러 개 선택)", "Control Tuning 작성",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }
        foreach (var it in selected)
        {
            it.GroupIndex = groupIndex;
            it.IsSelected = false;
        }
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
    private readonly FraHistoryViewModel _owner;

    public FraHistoryItemViewModel(FraHistorySample sample, FraHistoryViewModel owner)
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

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

    internal void RefreshGroupName() => OnPropertyChanged(nameof(GroupName));

    private static string F(double? v) => v?.ToString("0.00", CultureInfo.InvariantCulture) ?? "N/A";
}
