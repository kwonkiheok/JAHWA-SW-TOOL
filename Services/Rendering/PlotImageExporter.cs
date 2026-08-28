using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ScottPlot;
using ScottPlot.TickGenerators;

namespace JahwaInspector.Light.Services;

public static class PlotImageExporter
{
    /// <summary>
    /// 여러 CSV 결과를 오버레이한 Bode Plot을 PNG로 저장합니다.
    /// - X: Frequency(Hz) log10 변환 + 커스텀 tick
    /// - Left Y: Gain(dB) [-60, 60]
    /// - Right Y: Phase(deg) [-180, 180]
    /// </summary>
    public static void SaveBodeOverlayPng(
        IReadOnlyList<(string name, IReadOnlyList<BodePoint> pts)> series,
        string outputPngPath,
        int width = 1400,
        int height = 900,
        string? overlayLabel = null,
        float? fixedLineWidth = null,
        double? fixedOpacity = null)
    {
        if (series.Count == 0)
            return;

        var plot = new ScottPlot.Plot();

        // 스타일
        plot.FigureBackground.Color = Colors.White;
        plot.DataBackground.Color = Colors.White;
        plot.Grid.MajorLineColor = Colors.Black.WithOpacity(.15);
        plot.Grid.MinorLineColor = Colors.Black.WithOpacity(.05);
        plot.Grid.MinorLineWidth = 1;

        plot.XLabel("Frequency [Hz]");
        plot.YLabel("Gain [dB]");

        // Right axis
        plot.Axes.Right.IsVisible = true;
        plot.Axes.Right.Label.Text = "Phase [deg]";
        plot.Axes.Bottom.TickLabelStyle.FontSize = 16;
        plot.Axes.Left.TickLabelStyle.FontSize = 16;
        plot.Axes.Right.TickLabelStyle.FontSize = 16;

        // 고정 범위
        plot.Axes.SetLimitsY(-60, 60);
        plot.Axes.SetLimitsY(-180, 180, plot.Axes.Right);

        // 0 dB 라인
        var h0 = plot.Add.HorizontalLine(0);
        h0.Color = Colors.IndianRed;
        h0.LinePattern = LinePattern.Dashed;

        // X 범위(로그 데이터 기준)
        var allFreq = series.SelectMany(s => s.pts)
            .Where(p => p.FrequencyHz > 0)
            .Select(p => p.FrequencyHz)
            .ToArray();

        if (allFreq.Length > 0)
        {
            double minX = Math.Log10(allFreq.Min());
            double maxX = Math.Log10(allFreq.Max());
            plot.Axes.SetLimitsX(minX, maxX);
        }

        ApplyLogTicks(plot);

        // 오버레이
        int seriesCount = Math.Max(1, series.Count);
        float lineWidth = fixedLineWidth ?? (seriesCount <= 3 ? 2.4f : seriesCount <= 8 ? 1.8f : 1.2f);
        double alpha = fixedOpacity ?? (seriesCount <= 3 ? 0.95 : seriesCount <= 8 ? 0.75 : 0.45);
        alpha = Math.Clamp(alpha, 0.0, 1.0);

        foreach (var (_, pts) in series)
        {
            var valid = pts.Where(p => p.FrequencyHz > 0).ToArray();
            if (valid.Length == 0) continue;

            var xs = valid.Select(p => Math.Log10(p.FrequencyHz)).ToArray();
            var yg = valid.Select(p => p.GainDb).ToArray();
            var yp = valid.Select(p => WrapTo180(p.PhaseDeg)).ToArray();

            var gain = plot.Add.Scatter(xs, yg);
            gain.Color = Colors.Orange.WithOpacity(alpha);
            gain.LineWidth = lineWidth;
            gain.MarkerSize = 0;

            var phase = plot.Add.Scatter(xs, yp);
            phase.Color = Colors.Blue.WithOpacity(alpha);
            phase.LineWidth = lineWidth;
            phase.MarkerSize = 0;
            phase.Axes.YAxis = plot.Axes.Right;
        }

        plot.Legend.IsVisible = false;

        plot.SavePng(outputPngPath, width, height);
        DrawOverlayLabel(outputPngPath, overlayLabel);
    }

    /// <summary>
    /// 조건(그룹) 하나의 Settling 그래프를 그린다. PlotView에서 "조건" 드롭다운으로 단일 그룹을 확대해서
    /// 보여줄 때와 이미지로 저장할 때 둘 다 이 메서드를 써서 화면에 보이는 것과 저장되는 이미지가 항상 같게 한다.
    /// </summary>
    public static void ConfigureSettlingSubplot(
        ScottPlot.Plot plt,
        string groupLabel,
        IReadOnlyList<SettlingSeries> series,
        SettlingResult result,
        double displacement,
        double toleranceBand)
    {
        plt.FigureBackground.Color = Colors.White;
        plt.DataBackground.Color = Colors.White;
        plt.Grid.MajorLineColor = Colors.Black.WithOpacity(.12);
        plt.Grid.MinorLineColor = Colors.Black.WithOpacity(.04);
        plt.Axes.Right.IsVisible = false;

        string settleText = result.SettlingTimeMs is double st ? st.ToString("0.#") : "-";
        plt.Title(groupLabel);
        plt.XLabel($"Settle={settleText}ms  Disp={displacement:0.##}");

        double target = result.Target ?? 0;
        var hi = plt.Add.HorizontalLine(target + toleranceBand);
        hi.Color = Colors.IndianRed.WithOpacity(0.7);
        hi.LinePattern = LinePattern.Dashed;

        var lo = plt.Add.HorizontalLine(target - toleranceBand);
        lo.Color = Colors.IndianRed.WithOpacity(0.7);
        lo.LinePattern = LinePattern.Dashed;

        var t = plt.Add.HorizontalLine(target);
        t.Color = Colors.Black.WithOpacity(0.6);
        t.LinePattern = LinePattern.Dashed;

        var palette = new[] { Colors.Orange, Colors.Blue, Colors.Green, Colors.Purple, Colors.Brown };
        for (int i = 0; i < series.Count; i++)
        {
            var s = series[i];
            if (s.TimeMs.Count == 0) continue;

            var line = plt.Add.Scatter(s.TimeMs.ToArray(), s.Value.ToArray());
            line.Color = palette[i % palette.Length];
            line.LineWidth = 1.3f;
            line.MarkerSize = 0;
        }

        plt.Legend.IsVisible = false;
    }

    public static void SaveSettlingConditionPng(
        string path,
        string groupLabel,
        IReadOnlyList<SettlingSeries> series,
        SettlingResult result,
        double displacement,
        double toleranceBand,
        int width = 1000,
        int height = 650)
    {
        var plot = new ScottPlot.Plot();
        ConfigureSettlingSubplot(plot, groupLabel, series, result, displacement, toleranceBand);
        plot.SavePng(path, width, height);
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

    private static double WrapTo180(double deg)
    {
        var x = (deg + 180.0) % 360.0;
        if (x < 0) x += 360.0;
        return x - 180.0;
    }

    private static void DrawOverlayLabel(string pngPath, string? overlayLabel)
    {
        if (string.IsNullOrWhiteSpace(overlayLabel) || !File.Exists(pngPath))
            return;

        var text = overlayLabel.Trim();
        var bytes = File.ReadAllBytes(pngPath);
        using var inputStream = new MemoryStream(bytes);
        using var source = new Bitmap(inputStream);
        using var measureCanvas = new Bitmap(1, 1);
        using var measureGraphics = Graphics.FromImage(measureCanvas);
        float fontSize = 54f;
        System.Drawing.Font? bestFont = null;
        SizeF textSize = default;

        while (fontSize >= 20f)
        {
            bestFont?.Dispose();
            bestFont = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            textSize = measureGraphics.MeasureString(text, bestFont);
            if (textSize.Width <= source.Width - 40)
                break;
            fontSize -= 2f;
        }

        using (bestFont)
        using (var canvas = new Bitmap(source.Width, source.Height + Math.Max(64, (int)Math.Ceiling(textSize.Height + 26))))
        using (var g = Graphics.FromImage(canvas))
        using (var separatorPen = new Pen(System.Drawing.Color.FromArgb(224, 224, 224), 1f))
        using (var textBrush = new SolidBrush(System.Drawing.Color.Black))
        using (var shadowBrush = new SolidBrush(System.Drawing.Color.FromArgb(80, 255, 255, 255)))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.White);

            int headerHeight = canvas.Height - source.Height;
            g.DrawImage(source, 0, headerHeight, source.Width, source.Height);
            g.DrawLine(separatorPen, 0, headerHeight - 1, source.Width, headerHeight - 1);

            float x = Math.Max(20, (source.Width - textSize.Width) / 2f);
            float y = Math.Max(8f, (headerHeight - textSize.Height) / 2f);
            g.DrawString(text, bestFont!, shadowBrush, x + 1, y + 1);
            g.DrawString(text, bestFont!, textBrush, x, y);

            canvas.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
