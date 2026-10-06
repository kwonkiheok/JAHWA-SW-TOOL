using System.Globalization;

namespace MeasureHub.Services;

/// <summary>
/// System.Drawing(GDI+)으로 그래프 PNG를 직접 그리는 렌더러.
///
/// 예전에는 이 코드가 MainViewModel 안에 있었다. 화면 상태와 무관한 순수 그리기 로직인데
/// ViewModel 계층에 있어서 System.Drawing 참조가 114곳이나 잡혔고, 계층 구분이 흐려졌다.
/// 전부 static 이고 인스턴스 상태에 의존하지 않아 그대로 옮길 수 있었다.
///
/// 주의: 같은 "그림 그리기"라도 <see cref="PlotImageExporter"/> 는 ScottPlot 기반이고
/// 이 클래스는 GDI+ 기반이라 렌더링 스택이 다르다. 화면(PlotView)과 코드를 공유하는 쪽은
/// PlotImageExporter 이고, 이 클래스는 Export 전용 PNG 저장에만 쓰인다.
/// </summary>
public static class GdiPlotRenderer
{
    // DC 그래프 X/Y축은 파일마다 4000/5000으로 달라지지 않도록 0~5000(1000 간격)으로 고정한다.
    // 범위를 벗어난 점은 plotRect 로 클리핑된다.
    private static readonly List<double> DcFixedXTicks = [0, 1000, 2000, 3000, 4000, 5000];
    private static readonly List<double> DcFixedYTicks = [0, 1000, 2000, 3000, 4000, 5000];

    public static void SaveDcPlotPng(
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

        var xTicks = DcFixedXTicks;
        var yTicks = DcFixedYTicks;
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
        g.SetClip(plotRect);
        DrawSeries(g, upPen, upX, upY, plotRect, minX, maxX, minY, maxY);
        DrawSeries(g, downPen, downX, downY, plotRect, minX, maxX, minY, maxY);
        g.ResetClip();

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

    // 체크한 DC SENSITIVITY 파일 여러 개를 한 장에 겹쳐 그린다.
    // 파일마다 색을 바꾸고 Up은 실선, Down은 점선으로 구분한다.
    public static readonly System.Drawing.Color[] OverlayPalette =
    [
        System.Drawing.Color.FromArgb(0, 114, 189),
        System.Drawing.Color.FromArgb(217, 83, 25),
        System.Drawing.Color.FromArgb(237, 177, 32),
        System.Drawing.Color.FromArgb(126, 47, 142),
        System.Drawing.Color.FromArgb(119, 172, 48),
        System.Drawing.Color.FromArgb(77, 190, 238),
        System.Drawing.Color.FromArgb(162, 20, 47),
        System.Drawing.Color.FromArgb(70, 70, 70),
    ];

    public static void SaveDcOverlayPng(
        IReadOnlyList<(string Name, IReadOnlyList<double> UpX, IReadOnlyList<double> UpY, IReadOnlyList<double> DownX, IReadOnlyList<double> DownY)> series,
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
            plotMargin, plotMargin, bmp.Width - (plotMargin * 2), bmp.Height - (plotMargin * 2));

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.2f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(230, 230, 230), 1f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 9);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        var allX = series.SelectMany(s => s.UpX.Concat(s.DownX)).ToList();
        var allY = series.SelectMany(s => s.UpY.Concat(s.DownY)).ToList();
        if (allX.Count == 0 || allY.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double minX = allX.Min(), maxX = allX.Max();
        double minY = allY.Min(), maxY = allY.Max();
        if (Math.Abs(maxX - minX) < 1e-9) { minX -= 1; maxX += 1; }
        if (Math.Abs(maxY - minY) < 1e-9) { minY -= 1; maxY += 1; }

        var xTicks = DcFixedXTicks;
        var yTicks = DcFixedYTicks;
        minX = xTicks.Min(); maxX = xTicks.Max();
        minY = yTicks.Min(); maxY = yTicks.Max();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim();
            var ts = g.MeasureString(t, titleFont);
            g.DrawString(t, titleFont, textBrush, plotRect.Left + (plotRect.Width - ts.Width) / 2f, 18);
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

        g.SetClip(plotRect);
        for (int i = 0; i < series.Count; i++)
        {
            var color = OverlayPalette[i % OverlayPalette.Length];
            using var upPen = new System.Drawing.Pen(color, 2.0f);
            using var downPen = new System.Drawing.Pen(color, 2.0f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
            };
            DrawSeries(g, upPen, series[i].UpX, series[i].UpY, plotRect, minX, maxX, minY, maxY);
            DrawSeries(g, downPen, series[i].DownX, series[i].DownY, plotRect, minX, maxX, minY, maxY);
        }
        g.ResetClip();

        var xs = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xs.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ys = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ys.Width / 2f, 0);
        g.ResetTransform();

        // 범례: 파일명 (실선=Up, 점선=Down)
        float lx = plotRect.Left + 14;
        float ly = plotRect.Top + 12;
        g.DrawString("실선 = Up,  점선 = Down", legendFont, textBrush, lx, ly);
        ly += 18;
        for (int i = 0; i < series.Count; i++)
        {
            var color = OverlayPalette[i % OverlayPalette.Length];
            using var pen = new System.Drawing.Pen(color, 2.4f);
            g.DrawLine(pen, lx, ly + 7, lx + 26, ly + 7);
            g.DrawString(series[i].Name, legendFont, textBrush, lx + 32, ly);
            ly += 17;
            if (ly > plotRect.Bottom - 20) break;
        }

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    public static void SaveScanPlotPng(
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
                $"{DisplayFormat.Range(m.MinInput, m.MaxInput)}  Hys={m.Hys:0.###}, LI={m.Linearity:0.###}"));
            g.DrawString(info, labelFont, subTextBrush, plotRect.Left + 4, infoY);
        }

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    // X축과 Y축을 각각 별도 이미지 한 장씩으로 그린다. Posi(target, 빨강 점선)와 Hall(실제, 파랑 실선)을
    // 같은 Y 스케일로 겹쳐 그려 두 값이 얼마나 일치하는지 한눈에 보이게 한다.
    public static void SaveSinewaveAxisPlotPng(
        IReadOnlyList<double> time,
        IReadOnlyList<double> posi,
        IReadOnlyList<double> hall,
        string path,
        string title)
    {
        using var bmp = new System.Drawing.Bitmap(1400, 700);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int marginLeft = 140;
        const int marginRight = 40;
        const int marginTop = 90;
        const int marginBottom = 90;
        int plotWidth = bmp.Width - marginLeft - marginRight;
        int plotHeight = bmp.Height - marginTop - marginBottom;
        var rect = new System.Drawing.Rectangle(marginLeft, marginTop, plotWidth, plotHeight);

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.2f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(230, 230, 230), 1f);
        using var posiPen = new System.Drawing.Pen(System.Drawing.Color.Red, 0.9f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var hallPen = new System.Drawing.Pen(System.Drawing.Color.Blue, 1.4f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 9);
        using var legendFont = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        if (time.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = marginLeft + (plotWidth - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        double minX = time.Min();
        double maxX = time.Max();
        if (Math.Abs(maxX - minX) < 1e-9) { minX -= 1; maxX += 1; }
        var xTicks = BuildNiceTicks(minX, maxX, 8);
        minX = xTicks.Min();
        maxX = xTicks.Max();

        var values = posi.Concat(hall).ToList();
        double minY = values.Count > 0 ? values.Min() : -1;
        double maxY = values.Count > 0 ? values.Max() : 1;
        if (Math.Abs(maxY - minY) < 1e-9) { minY -= 1; maxY += 1; }
        var yTicks = BuildNiceTicks(minY, maxY, 6);
        minY = yTicks.Min();
        maxY = yTicks.Max();

        float maxYTickLabelWidth = 0;
        foreach (var yt in yTicks)
        {
            float y = MapY(yt, minY, maxY, rect);
            g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
            var label = FormatTick(yt);
            var size = g.MeasureString(label, labelFont);
            maxYTickLabelWidth = Math.Max(maxYTickLabelWidth, size.Width);
            g.DrawString(label, labelFont, textBrush, rect.Left - size.Width - 12, y - size.Height / 2);
        }
        foreach (var xt in xTicks)
        {
            float x = MapX(xt, minX, maxX, rect);
            g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);
            var label = FormatTick(xt);
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - size.Width / 2, rect.Bottom + 10);
        }
        g.DrawRectangle(axisPen, rect);
        DrawSeries(g, posiPen, time, posi, rect, minX, maxX, minY, maxY, drawMarkers: false);
        DrawSeries(g, hallPen, time, hall, rect, minX, maxX, minY, maxY, drawMarkers: false);

        var yLabel = "Code";
        float yLabelOffsetX = maxYTickLabelWidth + 45;
        g.TranslateTransform(rect.Left - yLabelOffsetX, rect.Top + rect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        var xLabel = "time[ms]";
        var xLabelSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, rect.Left + rect.Width / 2f - xLabelSize.Width / 2f, rect.Bottom + 40);

        float legendX = rect.Right - 150;
        float legendY = rect.Top + 10;
        g.DrawLine(posiPen, legendX, legendY + 7, legendX + 24, legendY + 7);
        g.DrawString("Target", legendFont, textBrush, legendX + 30, legendY);
        g.DrawLine(hallPen, legendX, legendY + 27, legendX + 24, legendY + 27);
        g.DrawString("Hall", legendFont, textBrush, legendX + 30, legendY + 20);

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    public static void SaveTiltPlotPng(
        IReadOnlyList<double> upTx,
        IReadOnlyList<double> upTy,
        IReadOnlyList<double> downTx,
        IReadOnlyList<double> downTy,
        string path,
        string title,
        double? specTilt)
    {
        using var bmp = new System.Drawing.Bitmap(1100, 1000);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int plotMargin = 100;
        int plotSize = Math.Min(bmp.Width, bmp.Height) - (plotMargin * 2);
        var plotRect = new System.Drawing.Rectangle(
            (bmp.Width - plotSize) / 2,
            plotMargin,
            plotSize,
            plotSize);

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.1f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 232, 232), 1f);
        using var crosshairPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 1f);
        using var specPen = new System.Drawing.Pen(System.Drawing.Color.IndianRed, 1.3f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var upPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 138, 33), 2.2f);
        using var downPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(42, 127, 239), 2.2f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        var allTx = upTx.Concat(downTx).ToList();
        var allTy = upTy.Concat(downTy).ToList();
        if (allTx.Count == 0 || allTy.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double maxAbs = allTx.Concat(allTy).Select(Math.Abs).DefaultIfEmpty(1).Max();
        if (specTilt is double specForRange)
            maxAbs = Math.Max(maxAbs, Math.Abs(specForRange));
        if (maxAbs <= 0 || double.IsNaN(maxAbs) || double.IsInfinity(maxAbs))
            maxAbs = 1;
        maxAbs *= 1.15;
        maxAbs = Math.Max(1, Math.Ceiling(maxAbs));

        double minX = -maxAbs, maxX = maxAbs, minY = -maxAbs, maxY = maxAbs;
        var ticks = new List<double>();
        for (double v = minX; v <= maxX + 0.5; v += 1)
            ticks.Add(v);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = plotRect.Left + (plotRect.Width - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        foreach (var t in ticks)
        {
            float y = MapY(t, minY, maxY, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            float x = MapX(t, minX, maxX, plotRect);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);

            var label = FormatTick(t);
            var sizeY = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, plotRect.Left - sizeY.Width - 12, y - sizeY.Height / 2);
            var sizeX = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - sizeX.Width / 2, plotRect.Bottom + 10);
        }

        g.DrawRectangle(axisPen, plotRect);

        float originX = MapX(0, minX, maxX, plotRect);
        float originY = MapY(0, minY, maxY, plotRect);
        g.DrawLine(crosshairPen, plotRect.Left, originY, plotRect.Right, originY);
        g.DrawLine(crosshairPen, originX, plotRect.Top, originX, plotRect.Bottom);

        if (specTilt is double specRadius && specRadius > 0)
        {
            float rx = MapX(specRadius, minX, maxX, plotRect) - originX;
            float ry = originY - MapY(specRadius, minY, maxY, plotRect);
            g.DrawEllipse(specPen, originX - rx, originY - ry, rx * 2, ry * 2);
        }

        DrawSeries(g, upPen, upTx, upTy, plotRect, minX, maxX, minY, maxY, drawMarkers: true);
        DrawSeries(g, downPen, downTx, downTy, plotRect, minX, maxX, minY, maxY, drawMarkers: true);

        const string xLabel = "Tx";
        const string yLabel = "Ty";
        var xSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xSize.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        float lx = plotRect.Right - 210;
        float ly = plotRect.Top + 15;
        g.DrawLine(upPen, lx, ly + 8, lx + 28, ly + 8);
        g.DrawString("Up", legendFont, textBrush, lx + 36, ly);
        g.DrawLine(downPen, lx, ly + 34, lx + 28, ly + 34);
        g.DrawString("Down", legendFont, textBrush, lx + 36, ly + 26);
        if (specTilt is not null)
        {
            g.DrawLine(specPen, lx, ly + 60, lx + 28, ly + 60);
            g.DrawString("Spec", legendFont, textBrush, lx + 36, ly + 52);
        }

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    // CIRCLE TEST: TILT의 4분면 산점도(SaveTiltPlotPng)와 동일한 방식이지만, 원점 대신 데이터 중심을
    // 기준으로 축 범위를 잡고 Up/Down 대신 Target(ReadVt) vs Hall을 겹쳐 그린다.
    public static void SaveCirclePlotPng(
        IReadOnlyList<double> readVtX,
        IReadOnlyList<double> readVtY,
        IReadOnlyList<double> hallX,
        IReadOnlyList<double> hallY,
        string path,
        string title)
    {
        using var bmp = new System.Drawing.Bitmap(1100, 1000);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int plotMargin = 100;
        int plotSize = Math.Min(bmp.Width, bmp.Height) - (plotMargin * 2);
        var plotRect = new System.Drawing.Rectangle(
            (bmp.Width - plotSize) / 2,
            plotMargin,
            plotSize,
            plotSize);

        using var axisPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1.1f);
        using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(232, 232, 232), 1f);
        using var crosshairPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 1f);
        using var targetPen = new System.Drawing.Pen(System.Drawing.Color.Red, 1.6f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var hallPen = new System.Drawing.Pen(System.Drawing.Color.Blue, 2.2f);
        using var titleFont = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
        using var labelFont = new System.Drawing.Font("Segoe UI", 11);
        using var legendFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
        using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);

        var allX = readVtX.Concat(hallX).ToList();
        var allY = readVtY.Concat(hallY).ToList();
        if (allX.Count == 0 || allY.Count == 0)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        double centerX = (allX.Max() + allX.Min()) / 2.0;
        double centerY = (allY.Max() + allY.Min()) / 2.0;
        double radius = Math.Max(
            allX.Select(v => Math.Abs(v - centerX)).DefaultIfEmpty(1).Max(),
            allY.Select(v => Math.Abs(v - centerY)).DefaultIfEmpty(1).Max());
        if (radius <= 0 || double.IsNaN(radius) || double.IsInfinity(radius))
            radius = 1;
        radius *= 1.15;

        double minX = centerX - radius, maxX = centerX + radius, minY = centerY - radius, maxY = centerY + radius;
        double tickStep = Math.Max(1, Math.Round(radius / 4));
        var ticks = new List<double>();
        for (double v = Math.Ceiling(minX / tickStep) * tickStep; v <= maxX; v += tickStep)
            ticks.Add(v);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleText = title.Trim();
            var titleSize = g.MeasureString(titleText, titleFont);
            float titleX = plotRect.Left + (plotRect.Width - titleSize.Width) / 2f;
            g.DrawString(titleText, titleFont, textBrush, titleX, 18);
        }

        foreach (var t in ticks)
        {
            float y = MapY(t, minY, maxY, plotRect);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            float x = MapX(t, minX, maxX, plotRect);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);

            var label = FormatTick(t);
            var sizeY = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, plotRect.Left - sizeY.Width - 12, y - sizeY.Height / 2);
            var sizeX = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, x - sizeX.Width / 2, plotRect.Bottom + 10);
        }

        g.DrawRectangle(axisPen, plotRect);

        float originX = MapX(centerX, minX, maxX, plotRect);
        float originY = MapY(centerY, minY, maxY, plotRect);
        g.DrawLine(crosshairPen, plotRect.Left, originY, plotRect.Right, originY);
        g.DrawLine(crosshairPen, originX, plotRect.Top, originX, plotRect.Bottom);

        DrawSeries(g, targetPen, readVtX, readVtY, plotRect, minX, maxX, minY, maxY, drawMarkers: false);
        DrawSeries(g, hallPen, hallX, hallY, plotRect, minX, maxX, minY, maxY, drawMarkers: false);

        const string xLabel = "X";
        const string yLabel = "Y";
        var xSize = g.MeasureString(xLabel, labelFont);
        g.DrawString(xLabel, labelFont, textBrush, plotRect.Left + plotRect.Width / 2f - xSize.Width / 2f, plotRect.Bottom + 40);

        g.TranslateTransform(30, plotRect.Top + plotRect.Height / 2f);
        g.RotateTransform(-90);
        var ySize = g.MeasureString(yLabel, labelFont);
        g.DrawString(yLabel, labelFont, textBrush, -ySize.Width / 2f, 0);
        g.ResetTransform();

        float lx = plotRect.Right - 210;
        float ly = plotRect.Top + 15;
        g.DrawLine(targetPen, lx, ly + 8, lx + 28, ly + 8);
        g.DrawString("Target", legendFont, textBrush, lx + 36, ly);
        g.DrawLine(hallPen, lx, ly + 34, lx + 28, ly + 34);
        g.DrawString("Hall", legendFont, textBrush, lx + 36, ly + 26);

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    public static (double min, double max) GetScanTightRange(double min, double max, double unit)
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

    public static List<double> BuildScanXTicks(double max)
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

    public static List<double> BuildTicksWithinRange(double min, double max, int targetCount)
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

    public static List<double> BuildNiceTicks(double min, double max, int targetCount)
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

    public static double NiceNumber(double value, bool round)
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

    public static string FormatTick(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 100 || Math.Abs(value - Math.Round(value)) < 1e-9)
            return value.ToString("0", CultureInfo.InvariantCulture);
        if (abs >= 10)
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static void DrawSeries(
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

    public static float MapX(double x, double minX, double maxX, System.Drawing.Rectangle rect)
        => rect.Left + (float)((x - minX) / (maxX - minX) * rect.Width);

    public static float MapY(double y, double minY, double maxY, System.Drawing.Rectangle rect)
        => rect.Bottom - (float)((y - minY) / (maxY - minY) * rect.Height);
}
