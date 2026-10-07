using System.IO;
using ClosedXML.Excel;

namespace MeasureHub.Services;

/// <summary>
/// DC Sensitivity History 시트에 빌드 블록을 하나씩 쌓는다. (공통 뼈대는 <see cref="HistoryBlocks"/>)
/// 축 행마다 대표 파형(시료 1개) 과 누적 파형(전체 겹침) 그래프 2장. 표는 넣지 않는다.
/// 그래프는 MeasureHub DC 렌더러(GdiPlotRenderer, 1400×900 고정) 를 그대로 쓰므로
/// 칸 크기를 그래프 비율(14:9)에 맞춰 빈 공간이 남지 않게 한다.
/// </summary>
public static class DcHistoryWriter
{
    private const int FirstCol = HistoryBlocks.FirstCol;
    private const int RepCol = HistoryBlocks.AxisCol + 1;   // 대표 파형
    private const int AllCol = RepCol + 1;                  // 누적 파형

    private const double GraphColWidth = 66;                // 문자 단위 ≈ 467px
    private const double GraphRowHeight = 230;              // pt ≈ 307px → 그래프 448×288px 가 94~96% 로 들어간다
    private const double EmptyRowHeight = 36;               // "측정 없음" 행
    private const double ImageW = 1400, ImageH = 900;       // GdiPlotRenderer DC 그래프 크기

    public static void AppendBuild(string xlsxPath, string sheetName, DcHistoryBuild build, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        HistoryBlocks.Append(xlsxPath, sheetName,
            ws => SetupSheet(ws, sheetName),
            (ws, startRow) => WriteBlock(ws, startRow, build, tempDir));
    }

    private static void SetupSheet(IXLWorksheet ws, string sheetName)
    {
        HistoryBlocks.SetupSheet(ws, $"{sheetName} — History",
            "대표 파형: 선택한 시료 1개 (실선 Up / 점선 Down)   |   누적 파형: 가져온 시료 전체 겹침   |   그래프 출처: MeasureHub DC Sensitivity 결과");

        ws.Cell(HistoryBlocks.HeadRow, RepCol).Value = "대표 파형";
        ws.Cell(HistoryBlocks.HeadRow, AllCol).Value = "누적 파형";
        ws.Column(RepCol).Width = GraphColWidth;
        ws.Column(AllCol).Width = GraphColWidth;
        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, FirstCol, HistoryBlocks.HeadRow, AllCol));
    }

    private static void WriteBlock(IXLWorksheet ws, int startRow, DcHistoryBuild build, string tempDir)
    {
        int row = startRow;
        foreach (var axisName in HistoryBlocks.AxisOrder)
        {
            var axis = build.Axes.FirstOrDefault(a => a.Axis == axisName && a.Samples.Count > 0);
            HistoryBlocks.WriteAxisLabel(ws, row, row, axisName);

            if (axis is null)
            {
                var r = ws.Range(row, RepCol, row, AllCol).Merge();
                r.Value = "측정 없음";
                r.Style.Font.SetFontColor(XLColor.Gray).Font.SetItalic()
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Row(row).Height = EmptyRowHeight;
            }
            else
            {
                ws.Row(row).Height = GraphRowHeight;
                AddGraphs(ws, row, axis, tempDir);
            }
            row++;
        }
        int blockEnd = row - 1;

        HistoryBlocks.WriteHeader(ws, startRow, blockEnd, build.Header);
        ws.Range(startRow, FirstCol, blockEnd, AllCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
    }

    private static void AddGraphs(IXLWorksheet ws, int row, DcHistoryAxis axis, string tempDir)
    {
        foreach (var col in new[] { RepCol, AllCol })
            ws.Cell(row, col).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);

        double colPx = HistoryBlocks.ColumnPx(GraphColWidth);
        double rowPx = HistoryBlocks.RowsPx(ws, row, row);
        double w = colPx * 0.96, h = w * ImageH / ImageW;
        if (h > rowPx * 0.96) { h = rowPx * 0.96; w = h * ImageW / ImageH; }

        var rep = axis.Representative;
        var repPng = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
        GdiPlotRenderer.SaveDcPlotPng(rep.UpInput, rep.UpOutput, rep.DownInput, rep.DownOutput, repPng,
            $"{axis.Axis} 대표 — {Path.GetFileNameWithoutExtension(rep.FileName)}", rep.InputLabel, rep.OutputLabel);
        HistoryBlocks.AddPicture(ws, repPng, row, RepCol, colPx, rowPx, w, h);

        var allPng = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
        GdiPlotRenderer.SaveDcOverlayPng(
            axis.Samples.Select(s => (Path.GetFileNameWithoutExtension(s.FileName), s.UpInput, s.UpOutput, s.DownInput, s.DownOutput)).ToList(),
            allPng, $"{axis.Axis} 누적 ({axis.Samples.Count}EA)", rep.InputLabel, rep.OutputLabel);
        HistoryBlocks.AddPicture(ws, allPng, row, AllCol, colPx, rowPx, w, h);
    }
}
