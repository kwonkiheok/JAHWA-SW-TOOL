namespace MeasureHub.ViewModels;

public sealed class SettlingConditionTolerance : ViewModelBase
{
    public string GroupLabel { get; }

    private string _toleranceBand;
    public string ToleranceBand { get => _toleranceBand; set { _toleranceBand = value; OnPropertyChanged(); } }

    public SettlingConditionTolerance(string groupLabel, string toleranceBand)
    {
        GroupLabel = groupLabel;
        _toleranceBand = toleranceBand;
    }
}
