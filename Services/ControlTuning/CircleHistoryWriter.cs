using System.IO;
using ClosedXML.Excel;

namespace MeasureHub.Services;

/// <summary>
/// Circle History 시트에 빌드 블록을 하나씩 쌓는다. (공통 뼈대는 <see cref="HistoryBlocks"/>, 구조는 Sine Wave 와 같다)
/// 6Hz / 10Hz 처럼 측정 조건마다 파일 묶음이 달라서 **측정 조건별로 행**을 나눈다. (H 열 = 조건 이름)
///   표 = 시료 행(그룹 순서) + Min/Max/Avg/STDEV, 값은 MaxErr X / MaxErr Y (code)
///   판정 = MeasureHub CIRCLE 결과 그대로, |MaxErr| 가 Spec(code)을 넘으면 빨간색
///   그래프 = 대표 시료의 XY 궤적 1장 (GdiPlotRenderer.SaveCirclePlotPng, 축 0~4096 고정)
/// um 환산(code / 4095 × stroke)은 정확하지 않아 넣지 않는다(사용자 결정).
/// 가져온 조건만 행이 생긴다.
/// </summary>
public static class CircleHistoryWriter
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
    private const int GraphCol = LastTableCol + 2;

    private const double RowHeight = 18;            // pt
    private const double MinConditionHeight = 240;  // pt, 원형 궤적이 너무 작지 않게
    private const double GraphColWidth = 50;        // 문자 단위
    private const double ImageW = 1100, ImageH = 1000;

    public static void AppendBuild(string xlsxPath, string sheetName, CircleHistoryBuild build, CircleHistorySpec spec, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        HistoryBlocks.Append(xlsxPath, sheetName,
            ws => SetupSheet(ws, sheetName, spec),
            (ws, startRow) => WriteBlock(ws, startRow, build, spec, tempDir));
    }

    private static readonly string[] Heads = ["Group", "No.", "File", "MaxErr X\n[code]", "MaxErr Y\n[code]", "Judge"];

    private static void SetupSheet(IXLWorksheet ws, string sheetName, CircleHistorySpec spec)
    {
        string specText = spec.MaxErr is double m ? $"|MaxErr| ≤ {m:0.##} code (초과·N/A 빨간색)" : "Spec 미지정";
        HistoryBlocks.SetupSheet(ws, $"{sheetName} — History",
            $"{specText}   |   행 = 측정 조건   |   그래프: 대표 시료(★)의 XY 궤적 (Target / Hall)   |   출처: MeasureHub CIRCLE TEST 결과");

        ws.Cell(HistoryBlocks.HeadRow, HistoryBlocks.AxisCol).Value = "조건";
        ws.Column(HistoryBlocks.AxisCol).Width = 12;

        string[] heads = Heads;
        double[] widths = [9, 5, 30, 10, 10, 7];
        for (int i = 0; i < heads.Length; i++)
        {
            ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).Value = heads[i];
            ws.Column(GroupCol + i).Width = widths[i];
        }
        ws.Column(LastTableCol + 1).Width = 2;
        ws.Cell(HistoryBlocks.HeadRow, GraphCol).Value = "대표 궤적 (XY)";
        ws.Column(GraphCol).Width = GraphColWidth;

        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, FirstCol, HistoryBlocks.HeadRow, LastTableCol));
        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, GraphCol, HistoryBlocks.HeadRow, GraphCol));
    }

    private static void WriteBlock(IXLWorksheet ws, int startRow, CircleHistoryBuild build, CircleHistorySpec spec, string tempDir)
    {
        EnsureHeadsMatch(ws);
        int row = startRow;
        foreach (var cond in build.Conditions.Take(MaxConditions))
        {
            int subStart = row;
            row = WriteConditionTable(ws, row, cond, spec);
            int subEnd = row - 1;

            HistoryBlocks.WriteAxisLabel(ws, subStart, subEnd, cond.Name);
            ws.Range(subStart, HistoryBlocks.AxisCol, subEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            AddGraph(ws, subStart, subEnd, cond, tempDir);
        }
        int blockEnd = row - 1;

        HistoryBlocks.WriteHeader(ws, startRow, blockEnd, build.Header);
        ws.Range(startRow, FirstCol, blockEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
        ws.Range(startRow, GraphCol, blockEnd, GraphCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
    }

    /// <summary>
    /// 헤더 행은 시트를 처음 만들 때만 쓰므로, 열 구성이 다른 예전 형식 시트에 추가하면 칸이 어긋난다.
    /// 그런 시트면 쓰지 않고 멈춘다(예외는 화면에서 경고로 보인다 → 저장되지 않음).
    /// </summary>
    private static void EnsureHeadsMatch(IXLWorksheet ws)
    {
        for (int i = 0; i < Heads.Length; i++)
        {
            string actual = ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).GetString().Replace("\r", "");
            if (actual != Heads[i])
                throw new InvalidOperationException(
                    $"'{ws.Name}' 시트의 열 구성이 현재 Circle 형식과 다릅니다(예전 형식으로 만든 시트).\n" +
                    $"칸이 어긋나므로 추가하지 않았습니다. 이 시트를 지우거나 다른 시트 이름 / 새 문서를 지정한 뒤 다시 추가하세요.\n\n" +
                    $"{HistoryBlocks.HeadRow}행 {ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).Address}: 기대 '{Heads[i].Replace("\n", " ")}', 실제 '{actual.Replace("\n", " ")}'");
        }
    }

    private static int WriteConditionTable(IXLWorksheet ws, int row, CircleHistoryCondition cond, CircleHistorySpec spec)
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

        // 시료가 적으면 궤적 그림이 너무 작아지므로 조건 행 높이를 최소값까지 늘린다(행 높이를 고르게).
        if ((row - first) * RowHeight < MinConditionHeight)
            ws.Rows(first, row - 1).Height = MinConditionHeight / (row - first);
        return row;
    }

    private static void WriteErr(IXLCell cell, double? v, CircleHistorySpec spec)
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

    /// <summary>대표 시료의 XY 궤적. 렌더러가 1100×1000 고정이라 칸 안에 비율을 지켜 맞춘다.</summary>
    private static void AddGraph(IXLWorksheet ws, int subStart, int subEnd, CircleHistoryCondition cond, string tempDir)
    {
        double colPx = HistoryBlocks.ColumnPx(GraphColWidth);
        double subPx = HistoryBlocks.RowsPx(ws, subStart, subEnd);
        double w = colPx * 0.96, h = w * ImageH / ImageW;
        if (h > subPx * 0.94) { h = subPx * 0.94; w = h * ImageW / ImageH; }

        ws.Range(subStart, GraphCol, subEnd, GraphCol).Merge().Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        var rep = cond.Representative;
        if (rep.ReadVtX.Count == 0 || rep.HallX.Count == 0) return;

        var png = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
        string title = $"{cond.Name.Replace('\n', ' ')} · {Path.GetFileNameWithoutExtension(rep.FileName)}";
        GdiPlotRenderer.SaveCirclePlotPng(rep.ReadVtX, rep.ReadVtY, rep.HallX, rep.HallY, png, title);
        HistoryBlocks.AddPicture(ws, png, subStart, GraphCol, colPx, subPx, w, h);
    }
}
