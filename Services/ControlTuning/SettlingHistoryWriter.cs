using System.IO;
using ClosedXML.Excel;

namespace MeasureHub.Services;

/// <summary>
/// Settling Time History 시트에 빌드 블록을 하나씩 쌓는다. (공통 뼈대는 <see cref="HistoryBlocks"/>)
/// 축 행마다:
///   표 = 머리글 행(조건 이름) + 시료 행(그룹 순서) + Min/Max/Avg/STDEV, 값은 조건별 Settling Time(ms)
///   그래프 = 대표 시료의 조건별 파형(앞 4개 조건), MeasureHub 의 SaveSettlingConditionPng 그대로
/// 조건은 장비·축마다 달라서 시트 머리글이 아니라 축마다 표 머리글 행에 쓴다.
/// </summary>
public static class SettlingHistoryWriter
{
    public const int MaxConditions = 8;
    public const int MaxGraphs = 4;

    private const int FirstCol = HistoryBlocks.FirstCol;
    private const int GroupCol = HistoryBlocks.AxisCol + 1;
    private const int NoCol = GroupCol + 1;
    private const int FileCol = GroupCol + 2;
    private const int CondCol = GroupCol + 3;                     // 조건 1 ~ 8
    private const int LastTableCol = CondCol + MaxConditions - 1;
    private const int GraphCol = LastTableCol + 2;

    private const double RowHeight = 18;            // pt
    private const double SubHeadHeight = 42;        // 조건 이름 머리글 행 (긴 조건 이름은 줄바꿈)
    private const double EmptyRowHeight = 36;
    private const double MinAxisHeight = 230;       // pt, 시료가 적어도 그래프가 너무 작지 않게
    private const double GraphColWidth = 62;

    public static void AppendBuild(string xlsxPath, string sheetName, SettlingHistoryBuild build, SettlingHistorySpec spec, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        HistoryBlocks.Append(xlsxPath, sheetName,
            ws => SetupSheet(ws, sheetName, spec),
            (ws, startRow) => WriteBlock(ws, startRow, build, spec, tempDir));
    }

    private static void SetupSheet(IXLWorksheet ws, string sheetName, SettlingHistorySpec spec)
    {
        string specText = spec.MaxSettlingTimeMs is double m ? $"Max Settling Time ≤ {m:0.##} ms (초과·N/A 빨간색)" : "Spec 미지정";
        HistoryBlocks.SetupSheet(ws, $"{sheetName} — History",
            $"{specText}   |   표: 조건별 Settling Time [ms]   |   그래프: 대표 시료의 조건별 파형   |   출처: MeasureHub SETTLING TIME 결과");

        ws.Cell(HistoryBlocks.HeadRow, GroupCol).Value = "Group / No. / File / 조건별 Settling Time [ms]  (조건 이름은 축마다 표 첫 행)";
        ws.Range(HistoryBlocks.HeadRow, GroupCol, HistoryBlocks.HeadRow, LastTableCol).Merge();
        ws.Column(GroupCol).Width = 9;
        ws.Column(NoCol).Width = 5;
        ws.Column(FileCol).Width = 26;
        for (int i = 0; i < MaxConditions; i++) ws.Column(CondCol + i).Width = 11;
        ws.Column(LastTableCol + 1).Width = 2;
        for (int g = 0; g < MaxGraphs; g++)
        {
            ws.Cell(HistoryBlocks.HeadRow, GraphCol + g).Value = $"대표 파형 {g + 1}";
            ws.Column(GraphCol + g).Width = GraphColWidth;
        }

        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, FirstCol, HistoryBlocks.HeadRow, LastTableCol));
        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, GraphCol, HistoryBlocks.HeadRow, GraphCol + MaxGraphs - 1));
    }

    private static void WriteBlock(IXLWorksheet ws, int startRow, SettlingHistoryBuild build, SettlingHistorySpec spec, string tempDir)
    {
        int row = startRow;
        foreach (var axisName in HistoryBlocks.AxisOrder)
        {
            var axis = build.Axes.FirstOrDefault(a => a.Axis == axisName && a.Groups.Any(g => g.Samples.Count > 0) && a.Conditions.Count > 0);
            int subStart = row;

            if (axis is null)
            {
                var r = ws.Range(row, GroupCol, row, LastTableCol).Merge();
                r.Value = "측정 없음";
                r.Style.Font.SetFontColor(XLColor.Gray).Font.SetItalic();
                ws.Row(row).Height = EmptyRowHeight;
                row++;
            }
            else
                row = WriteAxisTable(ws, row, axis, spec);
            int subEnd = row - 1;

            HistoryBlocks.WriteAxisLabel(ws, subStart, subEnd, axisName);
            ws.Range(subStart, HistoryBlocks.AxisCol, subEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            AddGraphs(ws, subStart, subEnd, axis, tempDir);
        }
        int blockEnd = row - 1;

        HistoryBlocks.WriteHeader(ws, startRow, blockEnd, build.Header);
        ws.Range(startRow, FirstCol, blockEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
        ws.Range(startRow, GraphCol, blockEnd, GraphCol + MaxGraphs - 1).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
    }

    private static int WriteAxisTable(IXLWorksheet ws, int row, SettlingHistoryAxis axis, SettlingHistorySpec spec)
    {
        var conds = axis.Conditions.Take(MaxConditions).ToList();

        // 축별 머리글 행: 조건 이름
        int headRow = row;
        ws.Cell(row, GroupCol).Value = "Group";
        ws.Cell(row, NoCol).Value = "No.";
        ws.Cell(row, FileCol).Value = "File";
        for (int c = 0; c < conds.Count; c++) ws.Cell(row, CondCol + c).Value = conds[c];
        ws.Range(row, GroupCol, row, LastTableCol).Style
            .Font.SetBold().Font.SetFontSize(9)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2"))
            .Alignment.SetWrapText().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Border.SetBottomBorder(XLBorderStyleValues.Thin);
        ws.Row(row).Height = SubHeadHeight;
        row++;

        int first = row, n = 0;
        foreach (var (groupName, samples) in axis.Groups)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                n++;
                ws.Cell(row, GroupCol).Value = i == 0 ? groupName : "";
                ws.Cell(row, NoCol).Value = $"#{n}";
                ws.Cell(row, FileCol).Value = ReferenceEquals(s, axis.Representative) ? $"★ {s.FileName}" : s.FileName;
                for (int c = 0; c < conds.Count; c++)
                {
                    var cell = ws.Cell(row, CondCol + c);
                    var d = s.Find(conds[c]);
                    if (d?.SettlingTimeMs is double v)
                    {
                        cell.Value = Math.Round(v, 2);
                        cell.Style.NumberFormat.Format = "0.00";
                        if (spec.MaxSettlingTimeMs is double max && v > max)
                            cell.Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
                    }
                    else
                    {
                        cell.Value = d is null ? "-" : "N/A";
                        if (d is not null) cell.Style.Font.SetFontColor(XLColor.Red);
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
            ws.Range(row, GroupCol, row, FileCol).Merge();
            for (int c = 0; c < conds.Count; c++)
            {
                var col = ws.Column(CondCol + c).ColumnLetter();
                ws.Cell(row, CondCol + c).FormulaA1 = $"IFERROR({fn}({col}{first}:{col}{lastData}),\"-\")";
                ws.Cell(row, CondCol + c).Style.NumberFormat.Format = "0.00";
            }
            ws.Range(row, GroupCol, row, LastTableCol).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2")).Font.SetBold();
            row++;
        }

        ws.Rows(first, row - 1).Height = RowHeight;
        ws.Range(first, GroupCol, row - 1, LastTableCol).Style
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Border.SetInsideBorder(XLBorderStyleValues.Hair);
        ws.Range(first, FileCol, lastData, FileCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

        // 시료가 적으면 그래프가 너무 작아지므로 축 높이를 최소값까지 늘린다(행 높이를 고르게).
        double total = SubHeadHeight + (row - first) * RowHeight;
        if (total < MinAxisHeight)
            ws.Rows(first, row - 1).Height = (MinAxisHeight - SubHeadHeight) / (row - first);

        return row;
    }

    /// <summary>대표 시료의 조건별 파형(앞 4개 조건)을 축 행 높이에 맞춰 그려 넣는다.</summary>
    private static void AddGraphs(IXLWorksheet ws, int subStart, int subEnd, SettlingHistoryAxis? axis, string tempDir)
    {
        double colPx = HistoryBlocks.ColumnPx(GraphColWidth);
        double subPx = HistoryBlocks.RowsPx(ws, subStart, subEnd);
        double w = colPx * 0.96, h = subPx * 0.94;

        for (int g = 0; g < MaxGraphs; g++)
        {
            ws.Range(subStart, GraphCol + g, subEnd, GraphCol + g).Merge()
              .Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            if (axis is null || g >= axis.Conditions.Count) continue;

            var d = axis.Representative.Find(axis.Conditions[g]);
            if (d is null || d.Series.Count == 0) continue;

            var png = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
            PlotImageExporter.SaveSettlingConditionPng(png,
                $"{axis.Axis} · {d.Label} · {Path.GetFileNameWithoutExtension(axis.Representative.FileName)}",
                d.Series, d.Result, d.Displacement, d.ToleranceBand, (int)(w * 2), (int)(h * 2));
            HistoryBlocks.AddPicture(ws, png, subStart, GraphCol + g, colPx, subPx, w, h);
        }
    }
}
