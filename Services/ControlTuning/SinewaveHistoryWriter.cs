using System.IO;
using ClosedXML.Excel;

namespace MeasureHub.Services;

/// <summary>
/// Sine Wave History 시트에 빌드 블록을 하나씩 쌓는다. (공통 뼈대는 <see cref="HistoryBlocks"/>)
/// Sine Wave 는 파일 하나에 X·Y 가 함께 있고 측정 조건(주파수·진폭)이 파일 묶음마다 달라서,
/// 축이 아니라 **측정 조건별로 행**을 나눈다. (H 열 = 조건 이름, 다른 시트의 Axis 자리)
///   표 = 시료 행(그룹 순서) + Min/Max/Avg/STDEV, 값은 MaxErr X / MaxErr Y
///   그래프 = 대표 시료의 [X]·[Y] 파형 (GdiPlotRenderer.SaveSinewaveAxisPlotPng, 1400×700 고정)
/// 가져온 조건만 행이 생긴다 ("측정 없음" 행은 만들지 않는다).
/// </summary>
public static class SinewaveHistoryWriter
{
    public const int MaxConditions = 4;

    private const int FirstCol = HistoryBlocks.FirstCol;
    private const int GroupCol = HistoryBlocks.AxisCol + 1;
    private const int NoCol = GroupCol + 1;
    private const int FileCol = GroupCol + 2;
    private const int ErrXCol = GroupCol + 3;
    private const int ErrYCol = GroupCol + 4;
    private const int JudgeCol = GroupCol + 5;
    private const int LastTableCol = JudgeCol;
    private const int GraphXCol = LastTableCol + 2;
    private const int GraphYCol = GraphXCol + 1;

    private const double RowHeight = 18;            // pt
    private const double MinConditionHeight = 190;  // pt, 시료가 적어도 그래프가 너무 작지 않게
    private const double GraphColWidth = 66;        // 문자 단위
    private const double ImageW = 1400, ImageH = 700;

    public static void AppendBuild(string xlsxPath, string sheetName, SinewaveHistoryBuild build, SinewaveHistorySpec spec, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        HistoryBlocks.Append(xlsxPath, sheetName,
            ws => SetupSheet(ws, sheetName, spec),
            (ws, startRow) => WriteBlock(ws, startRow, build, spec, tempDir));
    }

    private static void SetupSheet(IXLWorksheet ws, string sheetName, SinewaveHistorySpec spec)
    {
        string specText = spec.MaxErr is double m ? $"|MaxErr| ≤ {m:0.##} code (초과·N/A 빨간색)" : "Spec 미지정";
        HistoryBlocks.SetupSheet(ws, $"{sheetName} — History",
            $"{specText}   |   행 = 측정 조건   |   그래프: 대표 시료(★)의 [X]·[Y] 파형 (Target / Hall)   |   출처: MeasureHub SINE WAVE 결과");

        ws.Cell(HistoryBlocks.HeadRow, HistoryBlocks.AxisCol).Value = "조건";
        ws.Column(HistoryBlocks.AxisCol).Width = 12;

        string[] heads = ["Group", "No.", "File", "MaxErr X\n[code]", "MaxErr Y\n[code]", "Judge"];
        double[] widths = [9, 5, 28, 10, 10, 7];
        for (int i = 0; i < heads.Length; i++)
        {
            ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).Value = heads[i];
            ws.Column(GroupCol + i).Width = widths[i];
        }
        ws.Column(LastTableCol + 1).Width = 2;
        ws.Cell(HistoryBlocks.HeadRow, GraphXCol).Value = "[X] 대표 파형";
        ws.Cell(HistoryBlocks.HeadRow, GraphYCol).Value = "[Y] 대표 파형";
        ws.Column(GraphXCol).Width = GraphColWidth;
        ws.Column(GraphYCol).Width = GraphColWidth;

        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, FirstCol, HistoryBlocks.HeadRow, LastTableCol));
        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, GraphXCol, HistoryBlocks.HeadRow, GraphYCol));
    }

    private static void WriteBlock(IXLWorksheet ws, int startRow, SinewaveHistoryBuild build, SinewaveHistorySpec spec, string tempDir)
    {
        int row = startRow;
        foreach (var cond in build.Conditions.Take(MaxConditions))
        {
            int subStart = row;
            row = WriteConditionTable(ws, row, cond, spec);
            int subEnd = row - 1;

            HistoryBlocks.WriteAxisLabel(ws, subStart, subEnd, cond.Name);
            ws.Range(subStart, HistoryBlocks.AxisCol, subEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            AddGraphs(ws, subStart, subEnd, cond, tempDir);
        }
        int blockEnd = row - 1;

        HistoryBlocks.WriteHeader(ws, startRow, blockEnd, build.Header);
        ws.Range(startRow, FirstCol, blockEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
        ws.Range(startRow, GraphXCol, blockEnd, GraphYCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
    }

    private static int WriteConditionTable(IXLWorksheet ws, int row, SinewaveHistoryCondition cond, SinewaveHistorySpec spec)
    {
        int first = row, n = 0;
        foreach (var (groupName, samples) in cond.Groups)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                n++;
                ws.Cell(row, GroupCol).Value = i == 0 ? groupName : "";
                ws.Cell(row, NoCol).Value = $"#{n}";
                ws.Cell(row, FileCol).Value = ReferenceEquals(s, cond.Representative) ? $"★ {s.FileName}" : s.FileName;
                WriteErr(ws.Cell(row, ErrXCol), s.MaxErrX, spec);
                WriteErr(ws.Cell(row, ErrYCol), s.MaxErrY, spec);
                ws.Cell(row, JudgeCol).Value = s.Judge;
                if (s.Judge == "NG") ws.Cell(row, JudgeCol).Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
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
            foreach (var c in new[] { ErrXCol, ErrYCol })
            {
                var col = ws.Column(c).ColumnLetter();
                ws.Cell(row, c).FormulaA1 = $"IFERROR({fn}({col}{first}:{col}{lastData}),\"-\")";
                ws.Cell(row, c).Style.NumberFormat.Format = "0.0";
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

        // 시료가 적으면 그래프가 너무 작아지므로 조건 행 높이를 최소값까지 늘린다(행 높이를 고르게).
        if ((row - first) * RowHeight < MinConditionHeight)
            ws.Rows(first, row - 1).Height = MinConditionHeight / (row - first);
        return row;
    }

    private static void WriteErr(IXLCell cell, double? v, SinewaveHistorySpec spec)
    {
        if (v is double x)
        {
            cell.Value = Math.Round(x, 2);
            cell.Style.NumberFormat.Format = "0.0";
            if (spec.MaxErr is double max && Math.Abs(x) > max)
                cell.Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
        }
        else
        {
            cell.Value = "N/A";
            cell.Style.Font.SetFontColor(XLColor.Red);
        }
    }

    /// <summary>대표 시료의 [X]·[Y] 파형. 렌더러가 1400×700 고정이라 칸 안에 비율을 지켜 맞춘다.</summary>
    private static void AddGraphs(IXLWorksheet ws, int subStart, int subEnd, SinewaveHistoryCondition cond, string tempDir)
    {
        double colPx = HistoryBlocks.ColumnPx(GraphColWidth);
        double subPx = HistoryBlocks.RowsPx(ws, subStart, subEnd);
        double w = colPx * 0.96, h = w * ImageH / ImageW;
        if (h > subPx * 0.94) { h = subPx * 0.94; w = h * ImageW / ImageH; }

        var rep = cond.Representative;
        string stem = Path.GetFileNameWithoutExtension(rep.FileName);
        foreach (var (col, axis, posi, hall) in new[] { (GraphXCol, "X", rep.PosiX, rep.HallX), (GraphYCol, "Y", rep.PosiY, rep.HallY) })
        {
            ws.Range(subStart, col, subEnd, col).Merge().Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            if (rep.TimeMs.Count == 0 || posi.Count == 0) continue;

            var png = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
            GdiPlotRenderer.SaveSinewaveAxisPlotPng(rep.TimeMs, posi, hall, png, $"{cond.Name} · [{axis}] · {stem}");
            HistoryBlocks.AddPicture(ws, png, subStart, col, colPx, subPx, w, h);
        }
    }
}
