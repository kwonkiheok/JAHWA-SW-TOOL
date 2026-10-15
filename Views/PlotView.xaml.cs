using System;
using System.ComponentModel;
using System.Linq;
using MeasureHub.Services;
using MeasureHub.ViewModels;
using ScottPlot;
using ScottPlot.TickGenerators;
using ScottPlot.WPF;

namespace MeasureHub.Views;

public partial class PlotView : System.Windows.Controls.UserControl
{
    private MainViewModel? _vm;
    private bool _configured;
    private WpfPlot? _wpfPlot;
    private double? _fraMinLogX;
    private double? _fraMaxLogX;
    private double[]? _selectedFraFrequency;
    private double[]? _selectedFraGain;
    private double[]? _selectedFraPhaseRaw;

    public PlotView()
    {
        InitializeComponent();
        Loaded += (_, __) => Hook();
        DataContextChanged += (_, __) => Hook();
        SizeChanged += (_, __) => RefreshPlot();
    }

    private void Hook()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= VmOnPropertyChanged;
            _vm.SettlingSpec.PropertyChanged -= SettlingSpecOnPropertyChanged;
            _vm.SettlingSpec.ConditionTolerances.CollectionChanged -= SettlingConditionTolerancesChanged;
            foreach (var row in _vm.SettlingSpec.ConditionTolerances)
                row.PropertyChanged -= SettlingConditionTolerancePropertyChanged;
        }

        _vm = DataContext as MainViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += VmOnPropertyChanged;
            _vm.SettlingSpec.PropertyChanged += SettlingSpecOnPropertyChanged;
            _vm.SettlingSpec.ConditionTolerances.CollectionChanged += SettlingConditionTolerancesChanged;
            foreach (var row in _vm.SettlingSpec.ConditionTolerances)
                row.PropertyChanged += SettlingConditionTolerancePropertyChanged;
        }

        _configured = false;
        RefreshPlot();
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.PlotFilesFiltered)
            or nameof(MainViewModel.SelectedPlotFile)
            or nameof(MainViewModel.IsPlotTab)
            or nameof(MainViewModel.SelectedInspection)
            or nameof(MainViewModel.IsFraPhaseInverted))
        {
            Dispatcher.Invoke(RefreshPlot);
        }
    }

    // Tolerance/Tail Window을 바꾸면 Settling 그래프의 target·band·표시값도 실시간으로 다시 계산해서 보여준다.
    private void SettlingSpecOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null || !_vm.IsSettlingInspection) return;

        if (e.PropertyName is nameof(SettlingSpecViewModel.ToleranceBand)
            or nameof(SettlingSpecViewModel.TailWindowPercent))
        {
            Dispatcher.Invoke(RefreshPlot);
        }
    }

    // 조건별 Tolerance Band 테이블에 새 행이 추가되면 그 행의 편집도 구독하고,
    // 행이 추가되거나 값이 바뀔 때마다 그래프를 다시 그린다.
    private void SettlingConditionTolerancesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (SettlingConditionTolerance item in e.NewItems)
                item.PropertyChanged += SettlingConditionTolerancePropertyChanged;

        if (_vm?.IsSettlingInspection == true)
            Dispatcher.Invoke(RefreshPlot);
    }

    private void SettlingConditionTolerancePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm?.IsSettlingInspection == true)
            Dispatcher.Invoke(RefreshPlot);
    }

    private void EnsureConfiguredFra()
    {
        if (_configured) return;
        EnsurePlotControl();
        if (_wpfPlot is null) return;

        var plt = _wpfPlot.Plot;
        plt.FigureBackground.Color = Colors.White;
        plt.DataBackground.Color = Colors.White;
        plt.Grid.MajorLineColor = Colors.Black.WithOpacity(.15);
        plt.Grid.MinorLineColor = Colors.Black.WithOpacity(.05);
        plt.Grid.MinorLineWidth = 1;
        plt.XLabel("Frequency [Hz]");
        plt.YLabel("Gain [dB]");
        plt.Axes.Right.IsVisible = true;
        plt.Axes.Right.Label.Text = "Phase [deg]";
        plt.Axes.Bottom.TickLabelStyle.FontSize = 16;
        plt.Axes.Left.TickLabelStyle.FontSize = 16;
        plt.Axes.Right.TickLabelStyle.FontSize = 16;
        var h0 = plt.Add.HorizontalLine(0);
        h0.Color = Colors.IndianRed;
        h0.LinePattern = LinePattern.Dashed;
        plt.Axes.SetLimitsY(-60, 60);
        plt.Axes.SetLimitsY(-180, 180, plt.Axes.Right);
        _configured = true;
    }

    private void RefreshPlot()
    {
        if (!IsLoaded || _vm is null || !_vm.IsPlotTab) return;

        if (_vm.IsSinewaveInspection)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            RefreshSinewavePlot();
            return;
        }

        if (_vm.IsSquareInspection)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            RefreshSquarePlot();
            return;
        }

        EnsurePlotControl();
        if (_wpfPlot is null) return;

        if (_vm.IsSettlingInspection)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            RefreshSettlingPlot();
            return;
        }

        _wpfPlot.Plot.Clear();
        _configured = false;

        if (_vm.IsDcSensitivityInspection)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            RefreshDcPlot();
        }
        else if (_vm.IsScanInspection)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            RefreshScanPlot();
        }
        else if (_vm.IsTiltInspection)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            RefreshTiltPlot();
        }
        else if (_vm.IsCircleInspection)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            RefreshCirclePlot();
        }
        else
            RefreshFraPlot();

        _wpfPlot.Refresh();
    }

    private void RefreshFraPlot()
    {
        if (_wpfPlot is null || _vm is null) return;
        EnsureConfiguredFra();

        var allPts = _vm.PlotFilesFiltered.SelectMany(p => p.Points).Where(p => p.FrequencyHz > 0).ToList();
        if (allPts.Count == 0)
        {
            _fraMinLogX = null;
            _fraMaxLogX = null;
            ClearSelectedFraHoverCache();
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            return;
        }

        double minF = allPts.Min(p => p.FrequencyHz);
        double maxF = allPts.Max(p => p.FrequencyHz);
        double minX = Math.Log10(minF);
        double maxX = Math.Log10(maxF);
        _fraMinLogX = minX;
        _fraMaxLogX = maxX;
        _wpfPlot.Plot.Axes.SetLimits(minX, maxX, -60, 60);
        ApplyLogTicks(_wpfPlot.Plot);

        var active = _vm.ActivePlotFile;
        UpdateSelectedFraHoverCache(active);
        if (active is null)
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;

        foreach (var item in _vm.PlotFilesFiltered)
        {
            if (item.Points.Count == 0)
                continue;

            bool isActive = active is not null && ReferenceEquals(item, active);
            bool hasActive = active is not null;
            double lw = isActive ? 2.8 : 1.0;
            double alpha = hasActive ? (isActive ? 1.0 : 0.14) : 0.35;
            var xs = item.Points.Where(p => p.FrequencyHz > 0).Select(p => Math.Log10(p.FrequencyHz)).ToArray();
            var yg = item.Points.Where(p => p.FrequencyHz > 0).Select(p => p.GainDb).ToArray();
            var yp = item.Points.Where(p => p.FrequencyHz > 0).Select(p => WrapTo180(_vm.GetDisplayFraPhase(p.PhaseDeg))).ToArray();

            var gain = _wpfPlot.Plot.Add.Scatter(xs, yg);
            gain.Color = Colors.Orange.WithOpacity(alpha);
            gain.LineWidth = (float)lw;
            gain.MarkerSize = 0;

            var phase = _wpfPlot.Plot.Add.Scatter(xs, yp);
            phase.Color = Colors.Blue.WithOpacity(alpha);
            phase.LineWidth = (float)lw;
            phase.MarkerSize = 0;
            phase.Axes.YAxis = _wpfPlot.Plot.Axes.Right;
        }

        _wpfPlot.Plot.Legend.IsVisible = false;
    }

    private void RefreshDcPlot()
    {
        if (_wpfPlot is null || _vm is null) return;

        var plt = _wpfPlot.Plot;
        plt.FigureBackground.Color = Colors.White;
        plt.DataBackground.Color = Colors.White;
        plt.Grid.MajorLineColor = Colors.Black.WithOpacity(.15);
        plt.Grid.MinorLineColor = Colors.Black.WithOpacity(.05);
        plt.XLabel("Input");
        plt.YLabel("Output");
        plt.Axes.Right.IsVisible = false;

        var active = _vm.ActivePlotFile;
        var selected = _vm.SelectedPlotFile;
        IEnumerable<PlotFileItem> items = active is null
            ? _vm.PlotFilesFiltered.Where(x => x.IsDcSensitivity)
            : new PlotFileItem[] { active };
        var allX = items.SelectMany(x => x.DcUpInput.Concat(x.DcDownInput)).ToList();
        var allY = items.SelectMany(x => x.DcUpOutput.Concat(x.DcDownOutput)).ToList();
        if (allX.Count == 0 || allY.Count == 0)
            return;

        var minX = allX.Min();
        var maxX = allX.Max();
        var minY = allY.Min();
        var maxY = allY.Max();
        if (minX == maxX) { minX -= 1; maxX += 1; }
        if (minY == maxY) { minY -= 1; maxY += 1; }

        var (niceXMin, niceXMax, _) = GetNiceScale(minX, maxX, 6);
        var (niceYMin, niceYMax, _) = GetNiceScale(minY, maxY, 6);
        plt.Axes.SetLimits(niceXMin, niceXMax, niceYMin, niceYMax);

        plt.Axes.Bottom.TickGenerator = new NumericAutomatic
        {
            IntegerTicksOnly = true,
            LabelFormatter = x => x.ToString("0")
        };

        plt.Axes.Left.TickGenerator = new NumericAutomatic
        {
            IntegerTicksOnly = true,
            LabelFormatter = y => y.ToString("0")
        };

        foreach (var item in items)
        {
            bool isSelected = ReferenceEquals(item, active) || active is null;
            double lw = isSelected ? 2.5 : 1.2;
            double alpha = selected is null ? 0.35 : 1.0;

            if (item.DcUpInput.Count > 0)
            {
                var up = plt.Add.Scatter(item.DcUpInput.ToArray(), item.DcUpOutput.ToArray());
                up.Color = Colors.Orange.WithOpacity(alpha);
                up.LineWidth = (float)lw;
                up.MarkerSize = selected is null ? 0 : 3;
            }

            if (item.DcDownInput.Count > 0)
            {
                var down = plt.Add.Scatter(item.DcDownInput.ToArray(), item.DcDownOutput.ToArray());
                down.Color = Colors.Blue.WithOpacity(alpha);
                down.LineWidth = (float)lw;
                down.MarkerSize = selected is null ? 0 : 3;
            }
        }

        plt.Legend.IsVisible = false;
    }

    private void RefreshScanPlot()
    {
        if (_wpfPlot is null || _vm is null) return;

        var plt = _wpfPlot.Plot;
        plt.FigureBackground.Color = Colors.White;
        plt.DataBackground.Color = Colors.White;
        plt.Grid.MajorLineColor = Colors.Black.WithOpacity(.15);
        plt.Grid.MinorLineColor = Colors.Black.WithOpacity(.05);
        plt.XLabel("Input");
        plt.YLabel("Output");
        plt.Axes.Right.IsVisible = false;

        var active = _vm.ActivePlotFile;
        var selected = _vm.SelectedPlotFile;
        IEnumerable<PlotFileItem> items = active is null
            ? _vm.PlotFilesFiltered.Where(x => x.IsScan)
            : new PlotFileItem[] { active };

        var allX = items.SelectMany(x => x.ScanUpInput.Concat(x.ScanDownInput).Concat(x.ScanRefInput)).ToList();
        var allY = items.SelectMany(x => x.ScanUpOutput.Concat(x.ScanDownOutput).Concat(x.ScanRefOutput)).ToList();
        if (allX.Count == 0 || allY.Count == 0)
            return;

        var maxX = allX.Max();
        var minY = allY.Min();
        var maxY = allY.Max();
        var (scanXMin, scanXMax) = GetScanXRange(maxX);
        var useThousandYTicks = IsAkmScanDataType();
        var (scanYMin, scanYMax) = useThousandYTicks ? GetScanXRange(maxY) : GetScanYRangeFromZero(maxY, 100);
        plt.Axes.SetLimits(scanXMin, scanXMax, scanYMin, scanYMax);

        plt.Axes.Bottom.TickGenerator = new NumericAutomatic
        {
            IntegerTicksOnly = true,
            LabelFormatter = x => FormatScanXTickLabel(x, scanXMax)
        };

        plt.Axes.Left.TickGenerator = new NumericAutomatic
        {
            IntegerTicksOnly = true,
            LabelFormatter = useThousandYTicks
                ? y => FormatScan1000TickLabel(y, scanYMax)
                : y => y.ToString("0")
        };

        int idx = 0;
        foreach (var item in items)
        {
            bool isSelected = ReferenceEquals(item, active) || active is null;
            bool hasActive = active is not null;
            double lineWidth = isSelected ? 1.25 : 0.75;
            double alpha = hasActive ? (isSelected ? 0.98 : 0.16) : 0.55;
            float markerSize = selected is null ? 0 : 2.4f;

            if (item.ScanRefInput.Count > 1)
            {
                var reference = plt.Add.Scatter(item.ScanRefInput.ToArray(), item.ScanRefOutput.ToArray());
                reference.Color = Colors.Black.WithOpacity(Math.Max(0.25, alpha * 0.65));
                reference.LineWidth = 1;
                reference.MarkerSize = 0;
                if (idx == 0) reference.LegendText = "Reference";
            }

            if (item.ScanUpInput.Count > 0)
            {
                var up = plt.Add.Scatter(item.ScanUpInput.ToArray(), item.ScanUpOutput.ToArray());
                up.Color = Colors.Orange.WithOpacity(alpha);
                up.LineWidth = (float)lineWidth;
                up.MarkerSize = markerSize;
                if (idx == 0) up.LegendText = "Up";
            }

            if (item.ScanDownInput.Count > 0)
            {
                var down = plt.Add.Scatter(item.ScanDownInput.ToArray(), item.ScanDownOutput.ToArray());
                down.Color = Colors.Blue.WithOpacity(alpha);
                down.LineWidth = (float)lineWidth;
                down.MarkerSize = markerSize;
                if (idx == 0) down.LegendText = "Down";
            }

            idx++;
        }

        plt.Legend.IsVisible = true;
    }

    // TILT는 Target-vs-Output 곡선이 아니라 Tx(X축)-Ty(Y축) 평면의 4분면 산점도다.
    // 원점 대칭 범위로 축을 잡아 항상 4분면이 보이게 하고, Spec 반경을 점선 원으로 표시한다.
    private void RefreshTiltPlot()
    {
        if (_wpfPlot is null || _vm is null) return;

        var plt = _wpfPlot.Plot;
        plt.FigureBackground.Color = Colors.White;
        plt.DataBackground.Color = Colors.White;
        plt.Grid.MajorLineColor = Colors.Black.WithOpacity(.15);
        plt.Grid.MinorLineColor = Colors.Black.WithOpacity(.05);
        plt.XLabel("Tx");
        plt.YLabel("Ty");
        plt.Axes.Right.IsVisible = false;

        var active = _vm.ActivePlotFile;
        var selected = _vm.SelectedPlotFile;
        IEnumerable<PlotFileItem> items = active is null
            ? _vm.PlotFilesFiltered.Where(x => x.IsTilt)
            : new PlotFileItem[] { active };
        items = items.ToList();

        var allTx = items.SelectMany(x => x.TiltUpTx.Concat(x.TiltDownTx)).ToList();
        var allTy = items.SelectMany(x => x.TiltUpTy.Concat(x.TiltDownTy)).ToList();
        if (allTx.Count == 0 || allTy.Count == 0)
            return;

        double specTilt = ParseLiveDouble(_vm.TiltSpec.SpecTilt) ?? 0;
        double maxAbs = allTx.Concat(allTy).Select(Math.Abs).DefaultIfEmpty(1).Max();
        maxAbs = Math.Max(maxAbs, Math.Abs(specTilt));
        if (maxAbs <= 0 || double.IsNaN(maxAbs) || double.IsInfinity(maxAbs))
            maxAbs = 1;
        maxAbs *= 1.15;

        double niceMaxAbs = Math.Max(1, Math.Ceiling(maxAbs));

        plt.Axes.SetLimits(-niceMaxAbs, niceMaxAbs, -niceMaxAbs, niceMaxAbs);
        plt.Axes.Bottom.TickGenerator = new NumericFixedInterval(1)
        {
            LabelFormatter = x => x.ToString("0")
        };
        plt.Axes.Left.TickGenerator = new NumericFixedInterval(1)
        {
            LabelFormatter = x => x.ToString("0")
        };

        var h0 = plt.Add.HorizontalLine(0);
        h0.Color = Colors.Gray.WithOpacity(0.6);
        h0.LineWidth = 1;

        var v0 = plt.Add.VerticalLine(0);
        v0.Color = Colors.Gray.WithOpacity(0.6);
        v0.LineWidth = 1;

        if (specTilt > 0)
        {
            var circle = plt.Add.Circle(0, 0, specTilt);
            circle.LineColor = Colors.IndianRed.WithOpacity(0.7);
            circle.LineWidth = 1.3f;
            circle.LinePattern = LinePattern.Dashed;
            circle.FillColor = Colors.Transparent;
        }

        int idx = 0;
        foreach (var item in items)
        {
            bool isSelected = ReferenceEquals(item, active) || active is null;
            bool hasActive = active is not null;
            double lineWidth = isSelected ? 2.25 : 1.25;
            double alpha = hasActive ? (isSelected ? 0.98 : 0.16) : 0.55;
            float markerSize = selected is null ? 0 : 2.4f;

            if (item.TiltUpTx.Count > 0)
            {
                var up = plt.Add.Scatter(item.TiltUpTx.ToArray(), item.TiltUpTy.ToArray());
                up.Color = Colors.Orange.WithOpacity(alpha);
                up.LineWidth = (float)lineWidth;
                up.MarkerSize = markerSize;
                if (idx == 0) up.LegendText = "Up";
            }

            if (item.TiltDownTx.Count > 0)
            {
                var down = plt.Add.Scatter(item.TiltDownTx.ToArray(), item.TiltDownTy.ToArray());
                down.Color = Colors.Blue.WithOpacity(alpha);
                down.LineWidth = (float)lineWidth;
                down.MarkerSize = markerSize;
                if (idx == 0) down.LegendText = "Down";
            }

            idx++;
        }

        plt.Legend.IsVisible = true;
    }

    // ScottPlot의 Multiplot(2단 서브플롯)이 WPF 호스트 안에서 두 패널을 겹쳐 그리는 렌더링 버그가 있어,
    // Settling 그룹 그리드와 동일한 방식(WpfPlot 컨트롤을 축마다 별도로 생성)으로 대체했다.
    private void RefreshSinewavePlot()
    {
        if (_vm is null) return;

        _wpfPlot = null;
        PlotHost.Children.Clear();

        var active = _vm.ActivePlotFile;
        var item = active ?? _vm.PlotFilesFiltered.FirstOrDefault(x => x.IsSinewave);
        if (item is null || item.SinewaveTimeMs.Count == 0)
            return;

        double specMaxErr = ParseLiveDouble(_vm.SinewaveSpec.SpecMaxErr) ?? 0;

        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
        PlotHost.Children.Add(grid);

        var wpX = new WpfPlot { Margin = new System.Windows.Thickness(2) };
        System.Windows.Controls.Grid.SetRow(wpX, 0);
        grid.Children.Add(wpX);
        ConfigureSinewavePanel(wpX.Plot, "X-axis", item.SinewaveTimeMs, item.SinewavePosiX, item.SinewaveHallX, item.SinewaveDiffX, specMaxErr);
        wpX.Refresh();

        var wpY = new WpfPlot { Margin = new System.Windows.Thickness(2) };
        System.Windows.Controls.Grid.SetRow(wpY, 1);
        grid.Children.Add(wpY);
        ConfigureSinewavePanel(wpY.Plot, "Y-axis", item.SinewaveTimeMs, item.SinewavePosiY, item.SinewaveHallY, item.SinewaveDiffY, specMaxErr);
        wpY.Refresh();
    }

    // AKM(축 1개)/DW(축 2개: X,Y)처럼 파일마다 축 개수가 달라서 SINE WAVE와 같은 방식(축마다
    // 독립된 WpfPlot)을 개수에 맞게 동적으로 생성한다. Target vs Value 오버레이만 표시(Diff 없음).
    private void RefreshSquarePlot()
    {
        if (_vm is null) return;

        _wpfPlot = null;
        PlotHost.Children.Clear();

        var active = _vm.ActivePlotFile;
        var item = active ?? _vm.PlotFilesFiltered.FirstOrDefault(x => x.IsSquareTest);
        if (item is null || item.SquareAxes.Count == 0)
            return;

        var grid = new System.Windows.Controls.Grid();
        for (int i = 0; i < item.SquareAxes.Count; i++)
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
        PlotHost.Children.Add(grid);

        for (int i = 0; i < item.SquareAxes.Count; i++)
        {
            var axis = item.SquareAxes[i];
            var wp = new WpfPlot { Margin = new System.Windows.Thickness(2) };
            System.Windows.Controls.Grid.SetRow(wp, i);
            grid.Children.Add(wp);
            ConfigureSinewavePanel(wp.Plot, axis.Label, axis.TimeMs, axis.Target, axis.Value, Array.Empty<double>(), 0);
            wp.Refresh();
        }
    }

    // X축과 Y축 패널을 동일한 방식으로 구성한다. Posi(target)와 Hall(실제)을 같은 왼쪽 축 스케일로
    // 겹쳐 그리고, Diff는 오른쪽 축(+Spec 점선)에 표시한다.
    private static void ConfigureSinewavePanel(
        ScottPlot.Plot plt,
        string title,
        IReadOnlyList<double> time,
        IReadOnlyList<double> posi,
        IReadOnlyList<double> hall,
        IReadOnlyList<double> diff,
        double specMaxErr)
    {
        plt.FigureBackground.Color = Colors.White;
        plt.DataBackground.Color = Colors.White;
        plt.Grid.MajorLineColor = Colors.Black.WithOpacity(.15);
        plt.Grid.MinorLineColor = Colors.Black.WithOpacity(.05);
        plt.Title(title, 13);
        plt.XLabel("time[ms]");
        plt.YLabel("Code");
        plt.Axes.Left.Label.FontSize = 11;
        plt.Axes.Left.TickLabelStyle.FontSize = 10;
        plt.Axes.Bottom.Label.FontSize = 11;
        plt.Axes.Bottom.TickLabelStyle.FontSize = 10;
        plt.Legend.FontSize = 10;

        if (time.Count == 0) return;

        var posiLine = plt.Add.Scatter(time.ToArray(), posi.ToArray());
        posiLine.Color = Colors.Red;
        posiLine.LineWidth = 0.9f;
        posiLine.LinePattern = LinePattern.Dashed;
        posiLine.MarkerSize = 0;
        posiLine.LegendText = "Target";

        var hallLine = plt.Add.Scatter(time.ToArray(), hall.ToArray());
        hallLine.Color = Colors.Blue;
        hallLine.LineWidth = 1.4f;
        hallLine.MarkerSize = 0;
        hallLine.LegendText = "Hall";

        double minX = time.Min();
        double maxX = time.Max();
        plt.Axes.SetLimitsX(minX, maxX);

        plt.Legend.IsVisible = true;
    }

    // CIRCLE TEST는 시간에 따른 X/Y가 아니라 원을 그리는 궤적 자체가 핵심이므로,
    // TILT와 같은 방식(원점 대칭 4분면 XY 평면)으로 ReadVt(목표 원) vs Hall(실측 궤적)을 겹쳐 그린다.
    private void RefreshCirclePlot()
    {
        if (_wpfPlot is null || _vm is null) return;

        var plt = _wpfPlot.Plot;
        plt.FigureBackground.Color = Colors.White;
        plt.DataBackground.Color = Colors.White;
        plt.Grid.MajorLineColor = Colors.Black.WithOpacity(.15);
        plt.Grid.MinorLineColor = Colors.Black.WithOpacity(.05);
        plt.XLabel("X");
        plt.YLabel("Y");
        plt.Axes.Right.IsVisible = false;

        var active = _vm.ActivePlotFile;
        var item = active ?? _vm.PlotFilesFiltered.FirstOrDefault(x => x.IsCircleTest);
        if (item is null || item.CircleTimeMs.Count == 0)
            return;

        // 축은 Data Type 과 관계없이 PNG(GdiPlotRenderer.SaveCirclePlotPng)와 같은 0~4096(512 간격) 고정.
        const double axisMin = GdiPlotRenderer.CircleAxisMin;
        const double axisMax = GdiPlotRenderer.CircleAxisMax;
        double centerX = (axisMin + axisMax) / 2.0;
        double centerY = centerX;

        plt.Axes.SetLimits(axisMin, axisMax, axisMin, axisMax);
        plt.Axes.Bottom.TickGenerator = new NumericFixedInterval(GdiPlotRenderer.CircleAxisStep)
        {
            LabelFormatter = x => x.ToString("0")
        };
        plt.Axes.Left.TickGenerator = new NumericFixedInterval(GdiPlotRenderer.CircleAxisStep)
        {
            LabelFormatter = x => x.ToString("0")
        };

        var h0 = plt.Add.HorizontalLine(centerY);
        h0.Color = Colors.Gray.WithOpacity(0.6);
        h0.LineWidth = 1;

        var v0 = plt.Add.VerticalLine(centerX);
        v0.Color = Colors.Gray.WithOpacity(0.6);
        v0.LineWidth = 1;

        var targetLine = plt.Add.Scatter(item.CircleReadVtX.ToArray(), item.CircleReadVtY.ToArray());
        targetLine.Color = Colors.Red;
        targetLine.LineWidth = 1.2f;
        targetLine.LinePattern = LinePattern.Dashed;
        targetLine.MarkerSize = 0;
        targetLine.LegendText = "Target";

        var hallLine = plt.Add.Scatter(item.CircleHallX.ToArray(), item.CircleHallY.ToArray());
        hallLine.Color = Colors.Blue;
        hallLine.LineWidth = 1.6f;
        hallLine.MarkerSize = 0;
        hallLine.LegendText = "Hall";

        plt.Legend.IsVisible = true;
    }

    private static (double min, double max) GetScanXRange(double max)
    {
        if (double.IsNaN(max) || double.IsInfinity(max) || max <= 0)
            return (0, 100);

        return (0, max);
    }

    private static (double min, double max) GetScanYRangeFromZero(double max, double unit)
    {
        if (double.IsNaN(max) || double.IsInfinity(max) || max <= 0 || unit <= 0)
            return (0, 100);

        double roundedMax = Math.Ceiling(max / unit) * unit;
        if (roundedMax <= 0)
            roundedMax = unit;

        return (0, roundedMax);
    }

    private bool IsAkmScanDataType()
        => string.Equals(_vm?.ScanSpec.SelectedDataType, "AKM", StringComparison.OrdinalIgnoreCase);

    private static string FormatScanXTickLabel(double value, double max)
        => FormatScan1000TickLabel(value, max);

    private static string FormatScan1000TickLabel(double value, double max)
    {
        double rounded = Math.Round(value);
        if (Math.Abs(value - rounded) > 1e-6)
            return "";
        if (rounded < -1e-6 || rounded > max + 1e-6)
            return "";
        if (Math.Abs(rounded % 1000) > 1e-6)
            return "";

        return rounded.ToString("0");
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

    private const string SettlingAllGroupsOption = "전체 보기 (그리드)";

    // Settling은 단일 오버레이 대신 "동일 조건(그룹)"마다 독립된 서브플롯을 그린다.
    // 그룹 개수가 파일마다 달라서(DW: 2개, Robocube: N개) PlotHost 안에 UniformGrid로 WpfPlot을 동적으로 채운다.
    // 상단 "조건" 드롭다운에서 특정 그룹을 고르면 그 하나만 PlotHost 전체 크기로 확대해서 보여준다.
    // Tolerance Band/Tail Window는 스펙 값이 바뀔 때마다 여기서 즉시 재계산한다(값을 미리 구워두지 않음).
    private void RefreshSettlingPlot()
    {
        if (_vm is null) return;

        _wpfPlot = null;

        var active = _vm.ActivePlotFile;
        var source = active ?? _vm.PlotFilesFiltered.FirstOrDefault(x => x.IsSettling);
        var groups = source?.SettlingGroups ?? Array.Empty<(string GroupLabel, IReadOnlyList<SettlingSeries> Series)>();

        double defaultToleranceBand = ParseLiveDouble(_vm.SettlingSpec.ToleranceBand) ?? 0;
        double tailFraction = (ParseLiveDouble(_vm.SettlingSpec.TailWindowPercent) ?? 20) / 100.0;
        double judgeStartTimeMs = (_vm.SelectedSettlingVendorOption?.Handler ?? SettlingDataTypeRegistry.Get(null)).JudgeStartTimeMs;

        UpdateSettlingGroupSelectorItems(groups);

        PlotHost.Children.Clear();
        if (groups.Count == 0)
            return;

        var selectedLabel = SettlingGroupSelector.SelectedItem as string;
        if (!string.IsNullOrEmpty(selectedLabel) && selectedLabel != SettlingAllGroupsOption)
        {
            var one = groups.FirstOrDefault(g => g.GroupLabel == selectedLabel);
            if (one.Series is not null)
            {
                RenderSingleSettlingGroup(one, defaultToleranceBand, tailFraction, judgeStartTimeMs);
                return;
            }
        }

        RenderSettlingGrid(groups, defaultToleranceBand, tailFraction, judgeStartTimeMs);
    }

    // 조건(GroupLabel)별로 사용자가 지정한 Tolerance Band를 찾는다. 없으면 기본값을 그대로 쓴다.
    // (행 생성은 MainViewModel의 배치 계산 경로에서만 하므로 여기서는 읽기만 한다.)
    private double ResolveToleranceBand(string groupLabel, double defaultToleranceBand)
    {
        var row = _vm?.SettlingSpec.ConditionTolerances.FirstOrDefault(c => c.GroupLabel == groupLabel);
        return row is null ? defaultToleranceBand : (ParseLiveDouble(row.ToleranceBand) ?? defaultToleranceBand);
    }

    private void UpdateSettlingGroupSelectorItems(IReadOnlyList<(string GroupLabel, IReadOnlyList<SettlingSeries> Series)> groups)
    {
        var previous = SettlingGroupSelector.SelectedItem as string;
        var items = new System.Collections.Generic.List<string> { SettlingAllGroupsOption };
        items.AddRange(groups.Select(g => g.GroupLabel));

        SettlingGroupSelector.SelectionChanged -= SettlingGroupSelector_SelectionChanged;
        SettlingGroupSelector.ItemsSource = items;
        SettlingGroupSelector.SelectedItem = (previous is not null && items.Contains(previous)) ? previous : SettlingAllGroupsOption;
        SettlingGroupSelector.SelectionChanged += SettlingGroupSelector_SelectionChanged;
    }

    private void SettlingGroupSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshPlot();
    }

    private void RenderSettlingGrid(
        IReadOnlyList<(string GroupLabel, IReadOnlyList<SettlingSeries> Series)> groups,
        double defaultToleranceBand,
        double tailFraction,
        double judgeStartTimeMs)
    {
        int cols = (int)Math.Ceiling(Math.Sqrt(groups.Count));
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = cols };
        PlotHost.Children.Add(grid);

        foreach (var group in groups)
        {
            double toleranceBand = ResolveToleranceBand(group.GroupLabel, defaultToleranceBand);
            var (agg, displacement) = ComputeGroupLive(group.Series, toleranceBand, tailFraction, judgeStartTimeMs);
            var wp = new WpfPlot { Margin = new System.Windows.Thickness(2), MinHeight = 220 };
            PlotImageExporter.ConfigureSettlingSubplot(wp.Plot, group.GroupLabel, group.Series, agg, displacement, toleranceBand);
            wp.Refresh();
            grid.Children.Add(wp);
        }
    }

    private void RenderSingleSettlingGroup(
        (string GroupLabel, IReadOnlyList<SettlingSeries> Series) group,
        double defaultToleranceBand,
        double tailFraction,
        double judgeStartTimeMs)
    {
        double toleranceBand = ResolveToleranceBand(group.GroupLabel, defaultToleranceBand);
        var (agg, displacement) = ComputeGroupLive(group.Series, toleranceBand, tailFraction, judgeStartTimeMs);
        var wp = new WpfPlot { Margin = new System.Windows.Thickness(4) };
        PlotImageExporter.ConfigureSettlingSubplot(wp.Plot, group.GroupLabel, group.Series, agg, displacement, toleranceBand);
        wp.Refresh();
        PlotHost.Children.Add(wp);
    }

    private static (SettlingResult Result, double Displacement) ComputeGroupLive(
        IReadOnlyList<SettlingSeries> series, double toleranceBand, double tailFraction, double judgeStartTimeMs)
    {
        var perSeries = series
            .Select(s => SettlingMetrics.Compute(s.TimeMs, s.Value, s.ExplicitTarget, toleranceBand, tailFraction, judgeStartTimeMs))
            .ToList();
        var agg = SettlingMetrics.AggregateExcludingMinMax(perSeries);
        double displacement = series.Count > 0 ? series.Average(s => SettlingMetrics.ComputeDisplacement(s.Value, tailFraction)) : 0;
        return (agg, displacement);
    }

    private static double? ParseLiveDouble(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out v)) return v;
        return null;
    }

    private void ClearSelectedFraHoverCache()
    {
        _selectedFraFrequency = null;
        _selectedFraGain = null;
        _selectedFraPhaseRaw = null;
    }

    private void UpdateSelectedFraHoverCache(PlotFileItem? selected)
    {
        if (_vm is null || selected is null || selected.Points.Count == 0)
        {
            ClearSelectedFraHoverCache();
            return;
        }

        var valid = selected.Points
            .Where(p => p.FrequencyHz > 0)
            .OrderBy(p => p.FrequencyHz)
            .ToArray();

        if (valid.Length == 0)
        {
            ClearSelectedFraHoverCache();
            return;
        }

        _selectedFraFrequency = valid.Select(p => p.FrequencyHz).ToArray();
        _selectedFraGain = valid.Select(p => p.GainDb).ToArray();
        _selectedFraPhaseRaw = valid.Select(p => _vm.GetDisplayFraPhase(p.PhaseDeg)).ToArray();
    }

    private bool TryGetSelectedFraHoverValues(double logFrequency, out double frequencyHz, out double gainDb, out double phaseDeg)
    {
        frequencyHz = 0;
        gainDb = 0;
        phaseDeg = 0;

        if (_selectedFraFrequency is null || _selectedFraGain is null || _selectedFraPhaseRaw is null)
            return false;

        int n = _selectedFraFrequency.Length;
        if (n == 0 || _selectedFraGain.Length != n || _selectedFraPhaseRaw.Length != n)
            return false;

        double minLog = Math.Log10(_selectedFraFrequency[0]);
        double maxLog = Math.Log10(_selectedFraFrequency[n - 1]);
        double clampedLog = Math.Clamp(logFrequency, minLog, maxLog);
        frequencyHz = Math.Pow(10, clampedLog);
        if (double.IsNaN(frequencyHz) || double.IsInfinity(frequencyHz) || frequencyHz <= 0)
            return false;

        int idx = Array.BinarySearch(_selectedFraFrequency, frequencyHz);
        if (idx >= 0)
        {
            gainDb = _selectedFraGain[idx];
            phaseDeg = WrapTo180(_selectedFraPhaseRaw[idx]);
            return true;
        }

        int upper = ~idx;
        if (upper <= 0)
        {
            gainDb = _selectedFraGain[0];
            phaseDeg = WrapTo180(_selectedFraPhaseRaw[0]);
            return true;
        }

        if (upper >= n)
        {
            gainDb = _selectedFraGain[n - 1];
            phaseDeg = WrapTo180(_selectedFraPhaseRaw[n - 1]);
            return true;
        }

        int lower = upper - 1;
        double x0 = Math.Log10(_selectedFraFrequency[lower]);
        double x1 = Math.Log10(_selectedFraFrequency[upper]);
        double t = Math.Abs(x1 - x0) < 1e-12 ? 0.0 : (clampedLog - x0) / (x1 - x0);
        t = Math.Clamp(t, 0.0, 1.0);

        gainDb = _selectedFraGain[lower] + (_selectedFraGain[upper] - _selectedFraGain[lower]) * t;
        var rawPhase = _selectedFraPhaseRaw[lower] + (_selectedFraPhaseRaw[upper] - _selectedFraPhaseRaw[lower]) * t;
        phaseDeg = WrapTo180(rawPhase);
        return true;
    }

    private void EnsurePlotControl()
    {
        if (_wpfPlot is not null) return;
        _wpfPlot = new WpfPlot { Margin = new System.Windows.Thickness(0) };
        _wpfPlot.MouseMove += WpfPlotOnMouseMove;
        _wpfPlot.MouseLeave += WpfPlotOnMouseLeave;
        PlotHost.Children.Clear();
        PlotHost.Children.Add(_wpfPlot);
    }

    private void WpfPlotOnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_vm is null || _wpfPlot is null || !_vm.IsFraInspection || !_vm.IsPlotTab)
            return;

        var mouse = e.GetPosition(_wpfPlot);
        if (mouse.X < 0 || mouse.Y < 0 || mouse.X > _wpfPlot.ActualWidth || mouse.Y > _wpfPlot.ActualHeight)
        {
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            return;
        }

        if (!_fraMinLogX.HasValue || !_fraMaxLogX.HasValue || _wpfPlot.ActualWidth <= 0)
        {
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            return;
        }

        // Convert mouse pixel to plot coordinates using ScottPlot layout info
        // so axis padding/margins do not distort frequency mapping.
        var pixel = _wpfPlot.GetPlotPixelPosition(e);
        var coord = _wpfPlot.Plot.GetCoordinates(pixel, _wpfPlot.Plot.Axes.Bottom, _wpfPlot.Plot.Axes.Left);
        double logFrequency = coord.X;
        if (double.IsNaN(logFrequency) || double.IsInfinity(logFrequency))
        {
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            return;
        }
        logFrequency = Math.Clamp(logFrequency, _fraMinLogX.Value, _fraMaxLogX.Value);

        if (!TryGetSelectedFraHoverValues(logFrequency, out double frequencyHz, out double gainDb, out double phaseDeg))
        {
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
            return;
        }

        _vm.FraHoverFrequencyLabel = $"Freq: {frequencyHz:0.##} Hz | Gain: {gainDb:0.###} dB | Phase: {phaseDeg:0.###} deg";
    }

    private void WpfPlotOnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_vm is not null)
            _vm.FraHoverFrequencyLabel = MainViewModel.FraHoverInfoEmpty;
    }

    private static void ApplyLogTicks(ScottPlot.Plot plot)
    {
        static string LogTickLabelFormatter(double x) => $"{Math.Pow(10, x):N0}";
        var minorTickGen = new LogMinorTickGenerator();
        var tickGen = new NumericAutomatic
        {
            MinorTickGenerator = minorTickGen,
            IntegerTicksOnly = true,
            LabelFormatter = LogTickLabelFormatter,
        };
        plot.Axes.Bottom.TickGenerator = tickGen;
    }


    private static (double niceMin, double niceMax, double tickStep) GetNiceScale(double min, double max, int targetTickCount = 6)
    {
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max))
            return (0, 1, 0.2);

        if (min == max)
        {
            if (min == 0)
                return (0, 1, 0.2);

            double pad = Math.Abs(min) * 0.1;
            min -= pad;
            max += pad;
        }

        double range = NiceNum(max - min, false);
        double step = NiceNum(range / Math.Max(1, targetTickCount - 1), true);
        double niceMin = Math.Floor(min / step) * step;
        double niceMax = Math.Ceiling(max / step) * step;

        return (niceMin, niceMax, step);
    }

    private static double NiceNum(double value, bool round)
    {
        if (value == 0)
            return 1;

        double exponent = Math.Floor(Math.Log10(Math.Abs(value)));
        double fraction = Math.Abs(value) / Math.Pow(10, exponent);
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

    private static double WrapTo180(double deg)
    {
        var x = (deg + 180.0) % 360.0;
        if (x < 0) x += 360.0;
        return x - 180.0;
    }

    private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
    {

    }
}
