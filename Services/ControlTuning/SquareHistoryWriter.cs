using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace MeasureHub.Services;

/// <summary>
/// Square(Center Return) History 시트에 빌드 블록을 하나씩 쌓는다. (공통 뼈대는 <see cref="HistoryBlocks"/>, 구조는 Sine Wave 와 같다)
/// 30Hz / 5Hz Square 처럼 측정 조건마다 파일 묶음이 달라서 **측정 조건별로 행**을 나눈다. (H 열 = 조건 이름)
///   표 = 시료 행(그룹 순서) + Min/Max/Avg/STDEV
///        값 = Settling Up / Down [ms], Overshoot Up / Down [code] (양식 Test_OIS Center Return 과 같은 Up/Down 구분)
///        AKM 은 Up/Down 합산값 하나뿐이라 Up·Down 칸을 병합해 한 번만 보이게 한다(통계용으로 Down 칸에도 같은 값을 둔다).
///   판정 = MeasureHub SQUARE 결과 그대로, Spec(Settling ms / Overshoot code)을 넘으면 빨간색
///   그래프 = 대표 시료의 [X]·[Y] 파형 (GdiPlotRenderer.SaveSinewaveAxisPlotPng, Target / Value)
///            DW 는 한 파일에 X·Y 가 있고, AKM 은 축 1개라 파일명의 X/Y 로 칸을 정한다.
/// </summary>
public static class SquareHistoryWriter
{
    public const int MaxConditions = 4;

    private const int FirstCol = HistoryBlocks.FirstCol;
    private const int GroupCol = HistoryBlocks.AxisCol + 1;
    private const int NoCol = GroupCol + 1;
    private const int FileCol = GroupCol + 2;
    private const int SetUpCol = GroupCol + 3;
    private const int SetDownCol = GroupCol + 4;
    private const int OverUpCol = GroupCol + 5;
    private const int OverDownCol = GroupCol + 6;
    private const int JudgeCol = GroupCol + 7;
    private const int LastTableCol = JudgeCol;
    private const int GraphXCol = LastTableCol + 2;
    private const int GraphYCol = GraphXCol + 1;

    private const double RowHeight = 18;            // pt
    private const double MinConditionHeight = 190;  // pt
    private const double GraphColWidth = 66;        // 문자 단위
    private const double ImageW = 1400, ImageH = 700;

    private static readonly string[] Heads =
        ["Group", "No.", "File", "Settling Up\n[ms]", "Settling Down\n[ms]", "Overshoot Up\n[code]", "Overshoot Down\n[code]", "Judge"];

    public static void AppendBuild(string xlsxPath, string sheetName, SquareHistoryBuild build, SquareHistorySpec spec, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        HistoryBlocks.Append(xlsxPath, sheetName,
            ws => SetupSheet(ws, sheetName, spec),
            (ws, startRow) => WriteBlock(ws, startRow, build, spec, tempDir));
    }

    private static void SetupSheet(IXLWorksheet ws, string sheetName, SquareHistorySpec spec)
    {
        string specText = string.Join(" / ", new[]
        {
            spec.MaxSettlingMs is double s ? $"Settling ≤ {s:0.##} ms" : null,
            spec.MaxOvershoot is double o ? $"|Overshoot| ≤ {o:0.##} code" : null,
        }.Where(x => x is not null));
        if (specText.Length == 0) specText = "Spec 미지정";
        HistoryBlocks.SetupSheet(ws, $"{sheetName} — History",
            $"{specText} (초과·N/A 빨간색)   |   행 = 측정 조건   |   AKM 은 Up/Down 합산값(병합 칸)   |   그래프: 대표 시료(★)의 [X]·[Y] 파형 (Target / Value)   |   출처: MeasureHub SQUARE TEST 결과");

        ws.Cell(HistoryBlocks.HeadRow, HistoryBlocks.AxisCol).Value = "조건";
        ws.Column(HistoryBlocks.AxisCol).Width = 12;

        double[] widths = [9, 5, 28, 10, 10, 11, 11, 7];
        for (int i = 0; i < Heads.Length; i++)
        {
            ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).Value = Heads[i];
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

    private static void WriteBlock(IXLWorksheet ws, int startRow, SquareHistoryBuild build, SquareHistorySpec spec, string tempDir)
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
            AddGraphs(ws, subStart, subEnd, cond, tempDir);
        }
        int blockEnd = row - 1;

        HistoryBlocks.WriteHeader(ws, startRow, blockEnd, build.Header);
        ws.Range(startRow, FirstCol, blockEnd, LastTableCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
        ws.Range(startRow, GraphXCol, blockEnd, GraphYCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
    }

    /// <summary>헤더 행은 시트를 처음 만들 때만 쓰므로, 열 구성이 다른 예전 형식 시트에는 쓰지 않고 멈춘다.</summary>
    private static void EnsureHeadsMatch(IXLWorksheet ws)
    {
        for (int i = 0; i < Heads.Length; i++)
        {
            string actual = ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).GetString().Replace("\r", "");
            if (actual != Heads[i])
                throw new InvalidOperationException(
                    $"'{ws.Name}' 시트의 열 구성이 현재 Square 형식과 다릅니다(예전 형식이거나 다른 검사 시트).\n" +
                    $"칸이 어긋나므로 추가하지 않았습니다. 이 시트를 지우거나 다른 시트 이름 / 새 문서를 지정한 뒤 다시 추가하세요.\n\n" +
                    $"{HistoryBlocks.HeadRow}행 {ws.Cell(HistoryBlocks.HeadRow, GroupCol + i).Address}: 기대 '{Heads[i].Replace("\n", " ")}', 실제 '{actual.Replace("\n", " ")}'");
        }
    }

    private static int WriteConditionTable(IXLWorksheet ws, int row, SquareHistoryCondition cond, SquareHistorySpec spec)
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

                WritePair(ws, row, SetUpCol, SetDownCol, s.SettlingUpMs, s.SettlingDownMs, s.Combined, spec.MaxSettlingMs);
                WritePair(ws, row, OverUpCol, OverDownCol, s.OvershootUp, s.OvershootDown, s.Combined, spec.MaxOvershoot);

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
            foreach (var c in new[] { SetUpCol, OverUpCol })
            {
                var col = ws.Column(c).ColumnLetter();
                ws.Cell(row, c).FormulaA1 = $"IFERROR({fn}({col}{first}:{col}{lastData}),\"-\")";
                ws.Cell(row, c).Style.NumberFormat.Format = "0.0";
            }
            // Down 열: 합산값(AKM) 행은 Up·Down 이 병합돼 Down 칸이 비어 있으므로 그 행은 Up 값을 쓴다(배열 수식).
            foreach (var (up, down) in new[] { (SetUpCol, SetDownCol), (OverUpCol, OverDownCol) })
            {
                string u = $"{ws.Column(up).ColumnLetter()}{first}:{ws.Column(up).ColumnLetter()}{lastData}";
                string d = $"{ws.Column(down).ColumnLetter()}{first}:{ws.Column(down).ColumnLetter()}{lastData}";
                ws.Range(row, down, row, down).FormulaArrayA1 = $"IFERROR({fn}(IF({d}=\"\",{u},{d})),\"-\")";
                ws.Cell(row, down).Style.NumberFormat.Format = "0.0";
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

        if ((row - first) * RowHeight < MinConditionHeight)
            ws.Rows(first, row - 1).Height = MinConditionHeight / (row - first);
        return row;
    }

    /// <summary>
    /// Up / Down 한 쌍을 쓴다. 합산값(AKM)이면 두 칸을 병합해 한 번만 보이게 한다.
    /// (병합된 Down 칸은 비므로, 아래 Down 통계는 빈 칸이면 Up 값을 쓰는 배열 수식으로 계산한다)
    /// </summary>
    private static void WritePair(IXLWorksheet ws, int row, int upCol, int downCol, double? up, double? down, bool combined, double? spec)
    {
        WriteValue(ws.Cell(row, upCol), up, spec);
        if (combined)
            ws.Range(row, upCol, row, downCol).Merge();
        else
            WriteValue(ws.Cell(row, downCol), down, spec);
    }

    private static void WriteValue(IXLCell cell, double? v, double? spec)
    {
        if (v is double x)
        {
            cell.Value = Math.Round(x, 2);
            cell.Style.NumberFormat.Format = "0.0";
            if (spec is double max && Math.Abs(x) > max)
                cell.Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
        }
        else
        {
            cell.Value = "N/A";
            cell.Style.Font.SetFontColor(XLColor.Red);
        }
    }

    /// <summary>
    /// 대표 시료의 [X]·[Y] 파형. DW 는 축 라벨(X/Y)로, AKM("Value" 축 1개)은 파일명의 X/Y 로 칸을 정한다.
    /// 렌더러가 1400×700 고정이라 칸 안에 비율을 지켜 맞춘다.
    /// </summary>
    private static void AddGraphs(IXLWorksheet ws, int subStart, int subEnd, SquareHistoryCondition cond, string tempDir)
    {
        double colPx = HistoryBlocks.ColumnPx(GraphColWidth);
        double subPx = HistoryBlocks.RowsPx(ws, subStart, subEnd);
        double w = colPx * 0.96, h = w * ImageH / ImageW;
        if (h > subPx * 0.94) { h = subPx * 0.94; w = h * ImageW / ImageH; }

        foreach (var col in new[] { GraphXCol, GraphYCol })
            ws.Range(subStart, col, subEnd, col).Merge().Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);

        var rep = cond.Representative;
        string stem = Path.GetFileNameWithoutExtension(rep.FileName);
        foreach (var axis in rep.Axes)
        {
            string label = AxisOf(axis.Label, rep.FileName);
            int col = label == "Y" ? GraphYCol : GraphXCol;
            if (axis.TimeMs.Count == 0) continue;

            var png = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
            string title = $"{cond.Name.Replace('\n', ' ')} · [{label}] · {stem}";
            GdiPlotRenderer.SaveSinewaveAxisPlotPng(axis.TimeMs, axis.Target, axis.Value, png, title);
            HistoryBlocks.AddPicture(ws, png, subStart, col, colPx, subPx, w, h);
        }
    }

    /// <summary>축 라벨이 X/Y 면 그대로, 아니면(AKM "Value") 파일명에서 X/Y 를 찾는다. 못 찾으면 X.</summary>
    public static string AxisOf(string axisLabel, string fileName)
    {
        var l = axisLabel.Trim().ToUpperInvariant();
        if (l is "X" or "Y") return l;
        var m = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"(?<![A-Za-z])([XY])(?=[\d_#\-]|$)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "X";
    }
}
