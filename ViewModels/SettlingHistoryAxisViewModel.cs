using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using MeasureHub.Services;

namespace MeasureHub.ViewModels;

/// <summary>Settling 조건 체크 항목 (표·그래프에 넣을 조건).</summary>
public sealed class SettlingConditionOption : ViewModelBase
{
    private readonly Action _changed;

    public SettlingConditionOption(string label, bool isChecked, Action changed)
    {
        Label = label;
        _isChecked = isChecked;
        _changed = changed;
    }

    public string Label { get; }

    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged(); _changed(); } }
}

/// <summary>
/// Settling 축 탭 하나 (OIS X / OIS Y / AF).
/// FRA 처럼 시료를 그룹에 지정하고, DC 처럼 대표 시료(★)를 고르며, 표·그래프에 넣을 조건을 체크한다.
/// </summary>
public sealed class SettlingHistoryAxisViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public SettlingHistoryAxisViewModel(string axis, ControlTuningViewModel owner)
    {
        Axis = axis;
        _owner = owner;
        foreach (var g in owner.GroupNames)
            g.NameChanged += () => { foreach (var it in Items) it.RefreshGroupName(); RaiseSummary(); };

        ImportCommand = new RelayCommand(Import);
        ClearCommand = new RelayCommand(() => { Items.Clear(); Conditions.Clear(); RaiseSummary(); });
        AssignGroupCommands = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
            .Select(i => new SelectionCommand(sel => Assign(i, sel))).ToArray();
        UnassignCommand = new SelectionCommand(sel => Assign(-1, sel));
        SetRepresentativeCommand = new SelectionCommand(SetRepresentative);
    }

    public string Axis { get; }
    public ObservableCollection<SettlingHistoryItemViewModel> Items { get; } = new();
    public ObservableCollection<SettlingConditionOption> Conditions { get; } = new();

    public RelayCommand ImportCommand { get; }
    public RelayCommand ClearCommand { get; }
    public SelectionCommand[] AssignGroupCommands { get; }
    public SelectionCommand UnassignCommand { get; }
    public SelectionCommand SetRepresentativeCommand { get; }

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
            var rep = Items.FirstOrDefault(x => x.IsRepresentative)?.FileName ?? "(없음)";
            int conds = Conditions.Count(c => c.IsChecked);
            return $"{Items.Count}개 · " + string.Join(", ", parts) + (none > 0 ? $" · 미지정 {none}" : "")
                 + $"   |   조건 {conds}개 선택 (최대 {SettlingHistoryWriter.MaxConditions}, 그래프는 앞 {SettlingHistoryWriter.MaxGraphs}개)   |   대표: {rep}";
        }
    }

    private void Import()
    {
        var samples = _owner.TakeCurrentSettlingResults();
        if (samples.Count == 0) return;

        Items.Clear();
        foreach (var s in samples)
            Items.Add(new SettlingHistoryItemViewModel(s, _owner, this));
        Items[0].IsRepresentative = true;      // 기본 대표 = 첫 번째 파일

        // 조건 목록: 파일들에 나온 순서대로. 처음 체크는 축에 맞춰 자동으로 정한다.
        var labels = samples.SelectMany(s => s.Conditions.Select(c => c.Label)).Distinct().ToList();
        var auto = DefaultChecked(labels);
        Conditions.Clear();
        foreach (var l in labels)
            Conditions.Add(new SettlingConditionOption(l, auto.Contains(l), OnConditionsChanged));
        OnConditionsChanged();
    }

    /// <summary>
    /// 처음 체크할 조건. OIS X 탭이면 이름에 X 가 단어로 든 조건, OIS Y 탭이면 Y (DW 의 X Up / Y Down 등).
    /// 축 구분이 없는 조건(Robocube, K-star 등)이거나 AF 탭이면 앞에서부터 최대 8개.
    /// </summary>
    private HashSet<string> DefaultChecked(List<string> labels)
    {
        string? token = Axis switch { "OIS X" => "X", "OIS Y" => "Y", _ => null };
        if (token is not null)
        {
            var hit = labels.Where(l => Regex.IsMatch(l, $@"(^|[^A-Za-z]){token}([^A-Za-z]|$)")).ToList();
            if (hit.Count > 0) return hit.Take(SettlingHistoryWriter.MaxConditions).ToHashSet();
        }
        return labels.Take(SettlingHistoryWriter.MaxConditions).ToHashSet();
    }

    private void OnConditionsChanged()
    {
        foreach (var it in Items) it.RefreshValues();
        RaiseSummary();
    }

    internal IReadOnlyList<string> CheckedConditions => Conditions.Where(c => c.IsChecked).Select(c => c.Label).ToList();

    private void Assign(int groupIndex, System.Collections.IList? selection)
    {
        var selected = selection?.OfType<SettlingHistoryItemViewModel>().ToList() ?? [];
        if (selected.Count == 0)
        {
            ControlTuningViewModel.Info("목록에서 시료를 먼저 선택하세요. (Ctrl/Shift 로 여러 개 선택, [전체 선택] 가능)");
            return;
        }
        foreach (var it in selected) it.GroupIndex = groupIndex;
        selection!.Clear();
        RaiseSummary();
    }

    private void SetRepresentative(System.Collections.IList? selection)
    {
        var picked = selection?.OfType<SettlingHistoryItemViewModel>().FirstOrDefault();
        if (picked is null)
        {
            ControlTuningViewModel.Info("목록에서 대표로 쓸 시료를 먼저 선택하세요.");
            return;
        }
        foreach (var it in Items) it.IsRepresentative = ReferenceEquals(it, picked);
        RaiseSummary();
    }

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>그룹이 지정된 시료가 없으면 null. 대표가 그룹 밖이면 그룹 안 첫 시료로 대신한다.</summary>
    internal SettlingHistoryAxis? BuildAxis(IReadOnlyList<string> conditions)
    {
        var groups = new List<(string, IReadOnlyList<SettlingHistorySample>)>();
        for (int i = 0; i < FraHistoryWriter.MaxGroups; i++)
        {
            var samples = Items.Where(x => x.GroupIndex == i).Select(x => x.Sample).ToList();
            if (samples.Count > 0) groups.Add((_owner.GroupNameAt(i), samples));
        }
        if (groups.Count == 0) return null;

        var rep = Items.FirstOrDefault(x => x.IsRepresentative && x.GroupIndex >= 0)?.Sample ?? groups[0].Item2[0];
        return new SettlingHistoryAxis(Axis, conditions, groups, rep);
    }
}

/// <summary>Settling 축 탭의 시료 행.</summary>
public sealed class SettlingHistoryItemViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;
    private readonly SettlingHistoryAxisViewModel _axis;

    public SettlingHistoryItemViewModel(SettlingHistorySample sample, ControlTuningViewModel owner, SettlingHistoryAxisViewModel axis)
    {
        Sample = sample;
        _owner = owner;
        _axis = axis;
    }

    public SettlingHistorySample Sample { get; }
    public string FileName => Sample.FileName;

    /// <summary>체크한 조건의 Settling Time 을 순서대로 이어 보여 준다.</summary>
    public string Values => string.Join("  /  ", _axis.CheckedConditions.Select(l =>
        Sample.Find(l) is { } d ? (d.SettlingTimeMs is double v ? v.ToString("0.0", CultureInfo.InvariantCulture) : "N/A") : "-"));

    /// <summary>체크한 조건 중 NG 가 하나라도 있으면 NG.</summary>
    public string Judge => _axis.CheckedConditions.Any(l => Sample.Find(l)?.Judge == "NG") ? "NG" : "PASS";

    private int _groupIndex = -1;
    public int GroupIndex
    {
        get => _groupIndex;
        set { _groupIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(GroupName)); }
    }
    public string GroupName => GroupIndex < 0 ? "(미지정)" : _owner.GroupNameAt(GroupIndex);

    private bool _isRepresentative;
    public bool IsRepresentative
    {
        get => _isRepresentative;
        set { _isRepresentative = value; OnPropertyChanged(); OnPropertyChanged(nameof(RepresentativeMark)); }
    }
    public string RepresentativeMark => IsRepresentative ? "★" : "";

    internal void RefreshGroupName() => OnPropertyChanged(nameof(GroupName));

    internal void RefreshValues()
    {
        OnPropertyChanged(nameof(Values));
        OnPropertyChanged(nameof(Judge));
    }
}
