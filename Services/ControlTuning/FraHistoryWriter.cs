using System.IO;
using ClosedXML.Excel;

namespace MeasureHub.Services;

/// <summary>
/// FRA History 시트에 빌드 블록을 하나씩 쌓는다. (공통 뼈대는 <see cref="HistoryBlocks"/>)
/// 축 행마다 가운데는 MeasureHub 결과표, 오른쪽은 그룹별 Bode 오버레이 그래프.
/// </summary>
public static class FraHistoryWriter
{
    public const int MaxGroups = 4;

    private const int FirstCol = HistoryBlocks.FirstCol;
    private const int GroupCol = HistoryBlocks.AxisCol + 1;
    private const int NoCol = GroupCol + 1;
    private const int FileCol = GroupCol + 2;
    private const int JudgeCol = GroupCol + 3;
    private const int ValueCol = GroupCol + 4;      // 10Hz Gain, GM, PM, -3dB PM
    private const int LastTableCol = ValueCol + 3;
    private const int GraphCol = LastTableCol + 2;  // 표 오른쪽 한 칸 띄우고 그래프

    private const double RowHeight = 18;            // pt
    private const double EmptyRowHeight = 36;       // "측정 없음" 행
    private const double GraphColWidth = 75;        // 문자 단위

    private static readonly string[] TableHeads = ["Group", "No.", "File", "Judge", "10Hz Gain\n[dB]", "GM\n[dB]", "PM\n[deg]", "-3dB PM\n[deg]"];
    private static readonly double[] TableWidths = [7, 5, 30, 7, 9, 8, 8, 9];

    /// <summary>xlsxPath 의 sheetName 시트 맨 아래에 build 블록을 추가한다. 그래프 PNG 는 tempDir 에 그린 뒤 넣는다.</summary>
    public static void AppendBuild(string xlsxPath, string sheetName, FraHistoryBuild build, FraHistorySpec spec, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        HistoryBlocks.Append(xlsxPath, sheetName,
            ws => SetupSheet(ws, sheetName, spec),
            (ws, startRow) => WriteBlock(ws, startRow, build, spec, tempDir));
    }

    private static void SetupSheet(IXLWorksheet ws, string sheetName, FraHistorySpec spec)
    {
        HistoryBlocks.SetupSheet(ws, $"{sheetName} — History",
            $"Spec (MeasureHub): GM ≥ {Fmt(spec.Gm)} dB, PM ≥ {Fmt(spec.Pm)}°, -3dB PM ≥ {Fmt(spec.Pm3)}°   |   수치·그래프 출처: MeasureHub FRA 결과");

        for (int i = 0; i < TableHeads.Length; i++)
        {
            ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).Value = TableHeads[i];
            ws.Column(GroupCol + i).Width = TableWidths[i];
        }
        for (int g = 0; g < MaxGroups; g++)
        {
            ws.Cell(HistoryBlocks.HeadRow, GraphCol + g).Value = $"Group {g + 1}  Bode Plot";
            ws.Column(GraphCol + g).Width = GraphColWidth;
        }
        ws.Column(LastTableCol + 1).Width = 2;

        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, FirstCol, HistoryBlocks.HeadRow, LastTableCol));
        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, GraphCol, HistoryBlocks.HeadRow, GraphCol + MaxGroups - 1));
    }

    private static void WriteBlock(IXLWorksheet ws, int startRow, FraHistoryBuild build, FraHistorySpec spec, string tempDir)
    {
        int row = startRow;
        foreach (var axisName in HistoryBlocks.AxisOrder)
        {
            var axis = build.Axes.FirstOrDefault(a => a.Axis == axisName);
            var groups = axis?.Groups.Where(g => g.Samples.Count > 0).ToList() ?? [];
            int subStart = row;

            if (groups.Count > 0)
                row = WriteAxisTable(ws, row, groups, spec);
            else
            {
                var r = ws.Range(row, GroupCol, row, LastTableCol).Merge();
                r.Value = "측정 없음";
                r.Style.Font.SetFontColor(XLColor.Gray).Font.SetItalic();
                ws.Row(row).Height = EmptyRowHeight;
                row++;
            }
            int subEnd = row - 1;

            HistoryBlocks.WriteAxisLabel(ws, subStart, subEnd, axisName);
            ws.Range(subStart, HistoryBlocks.AxisCol, subEnd, LastTableCol).Style
                .Border.SetInsideBorder(XLBorderStyleValues.Hair)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            ws.Range(subStart, GroupCol, subEnd, LastTableCol).Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            ws.Range(subStart, FileCol, subEnd, FileCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

            AddGroupGraphs(ws, subStart, subEnd, axisName, axis?.Groups ?? [], tempDir);
        }
        int blockEnd = row - 1;

        HistoryBlocks.WriteHeader(ws, startRow, blockEnd, build.Header);
        ws.Range(startRow, FirstCol, blockEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
        ws.Range(startRow, GraphCol, blockEnd, GraphCol + MaxGroups - 1).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
    }

    /// <summary>축 하나의 결과표(시료 행 + Min/Max/Avg/STDEV)를 쓰고 다음 행 번호를 돌려준다.</summary>
    private static int WriteAxisTable(IXLWorksheet ws, int row, IReadOnlyList<(string GroupName, IReadOnlyList<FraHistorySample> Samples)> groups, FraHistorySpec spec)
    {
        int first = row;
        int n = 0;
        foreach (var (groupName, samples) in groups)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                n++;
                ws.Cell(row, GroupCol).Value = i == 0 ? groupName : "";
                ws.Cell(row, NoCol).Value = $"#{n}";
                ws.Cell(row, FileCol).Value = s.FileName;
                ws.Cell(row, JudgeCol).Value = s.Judge;
                if (s.Judge == "NG") ws.Cell(row, JudgeCol).Style.Font.SetFontColor(XLColor.Red).Font.SetBold();

                (double? v, double? lim)[] values = [(s.Gain10Hz, null), (s.GM, spec.Gm), (s.PM, spec.Pm), (s.PM3, spec.Pm3)];
                for (int k = 0; k < values.Length; k++)
                {
                    var cell = ws.Cell(row, ValueCol + k);
                    if (values[k].v is double v)
                    {
                        cell.Value = Math.Round(v, 2);
                        cell.Style.NumberFormat.Format = "0.00";
                        if (values[k].lim is double lim && v < lim)
                            cell.Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
                    }
                    else
                    {
                        cell.Value = "N/A";
                        cell.Style.Font.SetFontColor(XLColor.Red);
                    }
                }
                if (i == 0 && row > first)
                    ws.Range(row, GroupCol, row, LastTableCol).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);
                row++;
            }
        }

        int lastData = row - 1;
        foreach (var (label, fn) in new[] { ("Min.", "MIN"), ("Max.", "MAX"), ("Avg.", "AVERAGE"), ("STDEV", "STDEV") })
        {
            ws.Cell(row, GroupCol).Value = label;
            ws.Range(row, GroupCol, row, JudgeCol).Merge();
            for (int k = 0; k < 4; k++)
            {
                var col = ws.Column(ValueCol + k).ColumnLetter();
                ws.Cell(row, ValueCol + k).FormulaA1 = $"IFERROR({fn}({col}{first}:{col}{lastData}),\"-\")";
                ws.Cell(row, ValueCol + k).Style.NumberFormat.Format = "0.00";
            }
            ws.Range(row, GroupCol, row, LastTableCol).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2")).Font.SetBold();
            row++;
        }
        ws.Rows(first, row - 1).Height = RowHeight;
        return row;
    }

    /// <summary>
    /// 축 행 높이에 맞춘 병합 칸에 그룹별 Bode 오버레이를 넣는다.
    /// 그래프는 칸과 같은 비율로 그려야 빈 공간이 남지 않는다 (2배 해상도로 렌더링).
    /// </summary>
    private static void AddGroupGraphs(IXLWorksheet ws, int subStart, int subEnd, string axisName,
        IReadOnlyList<(string GroupName, IReadOnlyList<FraHistorySample> Samples)> groups, string tempDir)
    {
        double colPx = HistoryBlocks.ColumnPx(GraphColWidth);
        double subPx = HistoryBlocks.RowsPx(ws, subStart, subEnd);
        double w = colPx * 0.96, h = subPx * 0.94;

        for (int g = 0; g < MaxGroups; g++)
        {
            ws.Range(subStart, GraphCol + g, subEnd, GraphCol + g).Merge()
              .Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            if (g >= groups.Count || groups[g].Samples.Count == 0) continue;
            var (groupName, samples) = groups[g];

            var png = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
            PlotImageExporter.SaveBodeOverlayPng(
                samples.Select(s => (Path.GetFileNameWithoutExtension(s.FileName), s.Points)).ToList(),
                png, (int)(w * 2), (int)(h * 2),
                overlayLabel: $"{axisName} {groupName} ({samples.Count}EA)");
            HistoryBlocks.AddPicture(ws, png, subStart, GraphCol + g, colPx, subPx, w, h);
        }
    }

    private static string Fmt(double? v) => v?.ToString("0.##") ?? "-";
}
