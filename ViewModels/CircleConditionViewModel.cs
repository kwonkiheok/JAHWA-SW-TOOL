using System.Collections.ObjectModel;
using MeasureHub.Services;

namespace MeasureHub.ViewModels;

/// <summary>
/// Circle 조건 탭 하나 (Test1 ~ Test4). Sine Wave 조건 탭과 같다.
/// 조건 이름을 적고(AKM 은 파일 머리, DW 는 파일명에서 자동 제안), 결과를 가져와 그룹을 지정하고 대표 시료(★)를 고른다.
/// </summary>
public sealed class CircleConditionViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public CircleConditionViewModel(string slot, ControlTuningViewModel owner)
    {
        Slot = slot;
        _owner = owner;
        foreach (var g in owner.GroupNames)
            g.NameChanged += () => { foreach (var it in Items) it.RefreshGroupName(); RaiseSummary(); };

        ImportCommand = new RelayCommand(Import);
        ClearCommand = new RelayCommand(() => { Items.Clear(); RaiseSummary(); });
        AssignGroupCommands = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
            .Select(i => new SelectionCommand(sel => Assign(i, sel))).ToArray();
        UnassignCommand = new SelectionCommand(sel => Assign(-1, sel));
        SetRepresentativeCommand = new SelectionCommand(SetRepresentative);
    }

    /// <summary>탭 이름 (Test1 ~ Test4).</summary>
    public string Slot { get; }

    private string _conditionText = "";
    /// <summary>조건 설명 (예: 6Hz 85%). 문서의 조건 칸에는 "Test1" 아래 줄에 들어간다.</summary>
    public string ConditionText { get => _conditionText; set { _conditionText = value; OnPropertyChanged(); OnPropertyChanged(nameof(Header)); } }

    public ObservableCollection<CircleHistoryItemViewModel> Items { get; } = new();

    public RelayCommand ImportCommand { get; }
    public RelayCommand ClearCommand { get; }
    public SelectionCommand[] AssignGroupCommands { get; }
    public SelectionCommand UnassignCommand { get; }
    public SelectionCommand SetRepresentativeCommand { get; }

    public string Header => Items.Count == 0 ? Slot : $"{Slot} ({Items.Count})";
    public string Summary
    {
        get
        {
            if (Items.Count == 0) return "가져온 결과 없음 (가져온 조건만 문서에 행이 생깁니다)";
            var parts = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
                .Select(i => (name: _owner.GroupNameAt(i), n: Items.Count(x => x.GroupIndex == i)))
                .Where(x => x.n > 0)
                .Select(x => $"{x.name} {x.n}");
            int none = Items.Count(x => x.GroupIndex < 0);
            var rep = Items.FirstOrDefault(x => x.IsRepresentative)?.FileName ?? "(없음)";
            return $"{Items.Count}개 · " + string.Join(", ", parts) + (none > 0 ? $" · 미지정 {none}" : "") + $"   |   대표: {rep}";
        }
    }

    private void Import()
    {
        var samples = _owner.TakeCurrentCircleResults();
        if (samples.Count == 0) return;
        Items.Clear();
        foreach (var s in samples)
            Items.Add(new CircleHistoryItemViewModel(s, _owner));
        Items[0].IsRepresentative = true;      // 기본 대표 = 첫 번째 파일

        // 조건 설명이 비어 있으면 파일 머리(AKM) 또는 파일명(DW)에서 읽은 값(가장 많이 나온 것)으로 채운다.
        if (string.IsNullOrWhiteSpace(ConditionText))
            ConditionText = samples.Select(s => s.ConditionHint).Where(h => h.Length > 0)
                .GroupBy(h => h).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "";
        RaiseSummary();
    }

    private void Assign(int groupIndex, System.Collections.IList? selection)
    {
        var selected = selection?.OfType<CircleHistoryItemViewModel>().ToList() ?? [];
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
        var picked = selection?.OfType<CircleHistoryItemViewModel>().FirstOrDefault();
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
    internal CircleHistoryCondition? BuildCondition()
    {
        var groups = new List<(string, IReadOnlyList<CircleHistorySample>)>();
        for (int i = 0; i < FraHistoryWriter.MaxGroups; i++)
        {
            var samples = Items.Where(x => x.GroupIndex == i).Select(x => x.Sample).ToList();
            if (samples.Count > 0) groups.Add((_owner.GroupNameAt(i), samples));
        }
        if (groups.Count == 0) return null;

        var rep = Items.FirstOrDefault(x => x.IsRepresentative && x.GroupIndex >= 0)?.Sample ?? groups[0].Item2[0];
        string name = string.IsNullOrWhiteSpace(ConditionText) ? Slot : $"{Slot}\n{ConditionText.Trim()}";
        return new CircleHistoryCondition(name, groups, rep);
    }
}

/// <summary>Circle 조건 탭의 시료 행.</summary>
public sealed class CircleHistoryItemViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public CircleHistoryItemViewModel(CircleHistorySample sample, ControlTuningViewModel owner)
    {
        Sample = sample;
        _owner = owner;
    }

    public CircleHistorySample Sample { get; }
    public string FileName => Sample.FileName;
    public string Judge => Sample.Judge;
    public string MaxErrX => Sample.MaxErrX?.ToString("0.##") ?? "N/A";
    public string MaxErrY => Sample.MaxErrY?.ToString("0.##") ?? "N/A";

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
}
