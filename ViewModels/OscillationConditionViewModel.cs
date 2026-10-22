using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using MeasureHub.Services;

namespace MeasureHub.ViewModels;

/// <summary>
/// 발진 조건 탭 하나 (Test1 ~ Test4).
/// 조건 이름을 적고, 파형 사진을 추가해 그룹(Cav1~4)을 지정하고 순서를 정한다.
/// 칸 제목은 그룹 안 순서대로 "SPL n" 이 자동으로 붙고, 목록에서 고치면 그 값이 남는다.
/// </summary>
public sealed class OscillationConditionViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public OscillationConditionViewModel(string slot, ControlTuningViewModel owner)
    {
        Slot = slot;
        _owner = owner;
        foreach (var g in owner.GroupNames)
            g.NameChanged += () => { foreach (var it in Items) it.RefreshGroupName(); RaiseSummary(); };

        AddPhotosCommand = new RelayCommand(AddPhotos);
        ClearCommand = new RelayCommand(() => { Items.Clear(); RaiseSummary(); });
        AssignGroupCommands = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
            .Select(i => new SelectionCommand(sel => Assign(i, sel))).ToArray();
        UnassignCommand = new SelectionCommand(sel => Assign(-1, sel));
        RemoveCommand = new SelectionCommand(Remove);
        MoveUpCommand = new SelectionCommand(sel => Move(sel, -1));
        MoveDownCommand = new SelectionCommand(sel => Move(sel, +1));
    }

    /// <summary>탭 이름 (Test1 ~ Test4).</summary>
    public string Slot { get; }

    private string _conditionText = "";
    /// <summary>조건 설명 (예: High Temp 65℃). 문서의 조건 칸에는 "Test1" 아래 줄에 들어간다.</summary>
    public string ConditionText { get => _conditionText; set { _conditionText = value; OnPropertyChanged(); OnPropertyChanged(nameof(Header)); } }

    public ObservableCollection<OscillationPhotoItemViewModel> Items { get; } = new();

    public RelayCommand AddPhotosCommand { get; }
    public RelayCommand ClearCommand { get; }
    public SelectionCommand[] AssignGroupCommands { get; }
    public SelectionCommand UnassignCommand { get; }
    public SelectionCommand RemoveCommand { get; }
    public SelectionCommand MoveUpCommand { get; }
    public SelectionCommand MoveDownCommand { get; }

    public string Header => Items.Count == 0 ? Slot : $"{Slot} ({Items.Count})";
    public string Summary
    {
        get
        {
            if (Items.Count == 0) return "사진 없음 (사진이 있는 조건만 문서에 행이 생깁니다)";
            var parts = Enumerable.Range(0, FraHistoryWriter.MaxGroups)
                .Select(i => (name: _owner.GroupNameAt(i), n: Items.Count(x => x.GroupIndex == i)))
                .Where(x => x.n > 0)
                .Select(x => $"{x.name} {x.n}");
            int none = Items.Count(x => x.GroupIndex < 0);
            return $"{Items.Count}장 · " + string.Join(", ", parts) + (none > 0 ? $" · 미지정 {none}" : "")
                   + "   |   SPL 제목은 목록에서 더블클릭해 고칠 수 있습니다";
        }
    }

    private void AddPhotos()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"{Slot} 발진 파형 사진 선택 (여러 장 선택 가능)",
            Filter = "이미지 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|모든 파일 (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        AddFiles(dlg.FileNames);
    }

    /// <summary>파일을 목록 끝에 추가한다(이미 있는 파일은 건너뛴다). 이미지가 아니면 알려 준다.</summary>
    internal void AddFiles(IEnumerable<string> files)
    {
        var bad = new List<string>();
        foreach (var f in files.OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
        {
            if (Items.Any(x => string.Equals(x.Path, f, StringComparison.OrdinalIgnoreCase))) continue;
            try
            {
                using var img = Image.FromFile(f);
                Items.Add(new OscillationPhotoItemViewModel(f, img.Width, img.Height, _owner));
            }
            catch { bad.Add(Path.GetFileName(f)); }
        }
        Renumber();
        if (bad.Count > 0)
            ControlTuningViewModel.Info("이미지로 읽을 수 없는 파일은 빼고 추가했습니다:\n" + string.Join("\n", bad));
    }

    private void Assign(int groupIndex, System.Collections.IList? selection)
    {
        var selected = selection?.OfType<OscillationPhotoItemViewModel>().ToList() ?? [];
        if (selected.Count == 0)
        {
            ControlTuningViewModel.Info("목록에서 사진을 먼저 선택하세요. (Ctrl/Shift 로 여러 개 선택, [전체 선택] 가능)");
            return;
        }
        foreach (var it in selected) it.GroupIndex = groupIndex;
        selection!.Clear();
        Renumber();
    }

    private void Remove(System.Collections.IList? selection)
    {
        var selected = selection?.OfType<OscillationPhotoItemViewModel>().ToList() ?? [];
        foreach (var it in selected) Items.Remove(it);
        Renumber();
    }

    /// <summary>선택한 사진들을 한 칸 위/아래로 옮긴다(목록 순서 = 그룹 안 순서).</summary>
    private void Move(System.Collections.IList? selection, int delta)
    {
        var selected = selection?.OfType<OscillationPhotoItemViewModel>().ToList() ?? [];
        if (selected.Count == 0) return;
        var ordered = (delta < 0 ? selected.OrderBy(Items.IndexOf) : selected.OrderByDescending(Items.IndexOf)).ToList();
        foreach (var it in ordered)
        {
            int i = Items.IndexOf(it), j = i + delta;
            if (j < 0 || j >= Items.Count || selected.Contains(Items[j])) continue;
            Items.Move(i, j);
        }
        Renumber();
    }

    /// <summary>제목을 직접 고치지 않은 사진에 그룹 안 순서대로 "SPL n" 을 다시 붙인다.</summary>
    private void Renumber()
    {
        for (int g = -1; g < FraHistoryWriter.MaxGroups; g++)
        {
            int n = 0;
            foreach (var it in Items.Where(x => x.GroupIndex == g))
                it.SetAutoLabel($"SPL {++n}");
        }
        RaiseSummary();
    }

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>그룹이 지정된 사진이 없으면 null.</summary>
    internal OscillationCondition? BuildCondition()
    {
        var groups = new List<(string, IReadOnlyList<OscillationPhoto>)>();
        for (int i = 0; i < FraHistoryWriter.MaxGroups; i++)
        {
            var photos = Items.Where(x => x.GroupIndex == i).Select(x => new OscillationPhoto(x.Path, x.Label)).ToList();
            if (photos.Count > 0) groups.Add((_owner.GroupNameAt(i), photos));
        }
        if (groups.Count == 0) return null;
        string name = string.IsNullOrWhiteSpace(ConditionText) ? Slot : $"{Slot}\n{ConditionText.Trim()}";
        return new OscillationCondition(name, groups);
    }
}

/// <summary>발진 조건 탭의 사진 행.</summary>
public sealed class OscillationPhotoItemViewModel : ViewModelBase
{
    private readonly ControlTuningViewModel _owner;

    public OscillationPhotoItemViewModel(string path, int width, int height, ControlTuningViewModel owner)
    {
        Path = path;
        PixelSize = $"{width} × {height}";
        _owner = owner;
    }

    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public string PixelSize { get; }

    private bool _labelEdited;
    private string _label = "";
    /// <summary>칸 제목. 사용자가 고치면 자동 번호가 더는 덮어쓰지 않는다(비우면 다시 자동).</summary>
    public string Label
    {
        get => _label;
        set { _labelEdited = !string.IsNullOrWhiteSpace(value); _label = value; OnPropertyChanged(); }
    }

    internal void SetAutoLabel(string label)
    {
        if (_labelEdited) return;
        _label = label;
        OnPropertyChanged(nameof(Label));
    }

    private int _groupIndex = -1;
    public int GroupIndex
    {
        get => _groupIndex;
        set { _groupIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(GroupName)); }
    }
    public string GroupName => GroupIndex < 0 ? "(미지정)" : _owner.GroupNameAt(GroupIndex);

    internal void RefreshGroupName() => OnPropertyChanged(nameof(GroupName));
}
