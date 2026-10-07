using System.IO;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace MeasureHub.Services;

/// <summary>
/// FRA History 시트에 빌드 블록을 하나씩 쌓는다.
/// 블록 = 빌드 1회, 블록 안은 OIS X / OIS Y / AF 축 행으로 나뉜다.
/// 왼쪽은 헤더(개발 단계~검토 의견), 가운데는 MeasureHub 결과표, 오른쪽은 그룹별 Bode 오버레이 그래프.
/// </summary>
public static class FraHistoryWriter
{
    public static readonly string[] AxisOrder = ["OIS X", "OIS Y", "AF"];
    public const int MaxGroups = 4;

    private const int HeadRow = 4;
    private const int FirstCol = 2;                 // B열
    private const int AxisCol = FirstCol + 6;       // Axis
    private const int GroupCol = FirstCol + 7;
    private const int NoCol = FirstCol + 8;
    private const int FileCol = FirstCol + 9;
    private const int JudgeCol = FirstCol + 10;
    private const int ValueCol = FirstCol + 11;     // 10Hz Gain, GM, PM, -3dB PM
    private const int LastTableCol = FirstCol + 14;
    private const int GraphCol = LastTableCol + 2;  // 표 오른쪽 한 칸 띄우고 그래프

    private const double RowHeight = 18;            // pt
    private const double EmptyRowHeight = 36;       // "측정 없음" 행
    private const double GraphColWidth = 75;        // 문자 단위

    private static readonly string[] Heads =
    [
        "개발 단계", "제작처", "제작일자", "검토 담당자", "검토 사유", "검토 의견",
        "Axis", "Group", "No.", "File", "Judge", "10Hz Gain\n[dB]", "GM\n[dB]", "PM\n[deg]", "-3dB PM\n[deg]"
    ];
    private static readonly double[] HeadWidths = [10, 8, 11, 11, 24, 24, 7, 7, 5, 30, 7, 9, 8, 8, 9];

    /// <summary>
    /// xlsxPath 의 sheetName 시트 맨 아래에 build 블록을 추가한다. 파일이나 시트가 없으면 새로 만든다.
    /// 그룹 그래프 PNG 는 tempDir 에 그린 뒤 넣는다.
    /// </summary>
    public static void AppendBuild(string xlsxPath, string sheetName, FraHistoryBuild build, FraHistorySpec spec, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        using var wb = File.Exists(xlsxPath) ? new XLWorkbook(xlsxPath) : new XLWorkbook();

        if (!wb.Worksheets.TryGetWorksheet(sheetName, out var ws))
        {
            ws = wb.Worksheets.Add(sheetName);
            SetupSheet(ws, sheetName, spec);
        }

        int startRow = Math.Max(HeadRow + 1, (ws.LastRowUsed()?.RowNumber() ?? HeadRow) + 2);
        if (startRow == HeadRow + 2) startRow = HeadRow + 1;   // 첫 블록은 머리글 바로 아래부터
        WriteBlock(ws, startRow, build, spec, tempDir);

        // 새 통합문서에 기본으로 생기는 빈 시트가 있으면 지운다.
        if (wb.Worksheets.Count > 1 && wb.Worksheets.TryGetWorksheet("Sheet1", out var empty) && empty.LastCellUsed() is null)
            empty.Delete();

        if (File.Exists(xlsxPath)) wb.Save();
        else wb.SaveAs(xlsxPath);
    }

    // ---------------- 기존 빌드 목록 / 헤더 편집 / 삭제 ----------------
    // 블록은 B열(개발 단계) 세로 병합 범위로 찾는다. 헤더 B~G 는 블록 전체에 병합돼 있으므로
    // 병합 범위 하나 = 블록 하나다. (별도 표시를 남기지 않아 이전에 만든 문서에도 그대로 쓸 수 있다.)

    public static IReadOnlyList<FraHistoryBlockInfo> ListBuilds(string xlsxPath, string sheetName)
    {
        if (!File.Exists(xlsxPath)) return [];
        using var wb = new XLWorkbook(xlsxPath);
        if (!wb.Worksheets.TryGetWorksheet(sheetName, out var ws)) return [];
        return FindBlocks(ws);
    }

    public static IReadOnlyList<string> ListSheets(string xlsxPath)
    {
        if (!File.Exists(xlsxPath)) return [];
        using var wb = new XLWorkbook(xlsxPath);
        return wb.Worksheets.Select(w => w.Name).ToList();
    }

    /// <summary>startRow 블록의 헤더 6칸(개발 단계 ~ 검토 의견)을 바꾼다.</summary>
    public static void UpdateHeader(string xlsxPath, string sheetName, int startRow, FraHistoryHeader header)
    {
        using var wb = new XLWorkbook(xlsxPath);
        var ws = wb.Worksheet(sheetName);
        var block = FindBlocks(ws).FirstOrDefault(b => b.StartRow == startRow)
            ?? throw new InvalidOperationException("해당 빌드를 문서에서 찾지 못했습니다. 목록을 새로 고친 뒤 다시 시도하세요.");
        string[] values = [header.Stage, header.Maker, header.Date, header.Owner, header.Reason, header.Opinion];
        for (int i = 0; i < values.Length; i++)
            ws.Cell(block.StartRow, FirstCol + i).Value = values[i];
        wb.Save();
    }

    /// <summary>startRow 블록(행 + 그 안의 그래프 그림)을 지우고 아래 블록을 위로 당긴다.</summary>
    public static void DeleteBuild(string xlsxPath, string sheetName, int startRow)
    {
        // ClosedXML 0.104 는 불러온 그림을 지운 뒤 같은 저장에서 그림을 새로 넣으면 파일이 깨진다(관계 ID 충돌).
        // 그래서 ① 남길 그림을 메모리에 기억 → 그림 전부 삭제 + 행 삭제 → 저장
        //       ② 다시 열어 남길 그림을 새 위치에 추가 → 저장
        // 두 번에 나눠 저장한다. 중간에 실패해도 원본이 깨지지 않도록 임시 복사본에서 작업한 뒤 바꿔 넣는다.
        var work = xlsxPath + ".tmp.xlsx";
        File.Copy(xlsxPath, work, true);
        var keep = new List<(byte[] image, int row, int col, int offX, int offY, int w, int h)>();
        try
        {
            using (var wb = new XLWorkbook(work))
            {
                var ws = wb.Worksheet(sheetName);
                var blocks = FindBlocks(ws);
                var block = blocks.FirstOrDefault(b => b.StartRow == startRow)
                    ?? throw new InvalidOperationException("해당 빌드를 문서에서 찾지 못했습니다. 목록을 새로 고친 뒤 다시 시도하세요.");

                // 블록 사이 빈 줄도 함께 지운다: 뒤에 블록이 있으면 뒤쪽 빈 줄, 마지막 블록이면 앞쪽 빈 줄.
                int delStart = block.StartRow, delEnd = block.EndRow;
                if (blocks.Any(b => b.StartRow > block.EndRow)) delEnd++;
                else if (blocks.Any(b => b.EndRow < block.StartRow)) delStart--;
                int count = delEnd - delStart + 1;

                foreach (var pic in ws.Pictures.ToList())
                {
                    int row = pic.TopLeftCell.Address.RowNumber;
                    if (row < delStart || row > delEnd)
                    {
                        using var ms = new MemoryStream();
                        pic.ImageStream.Position = 0;
                        pic.ImageStream.CopyTo(ms);
                        var off = pic.GetOffset(XLMarkerPosition.TopLeft);
                        keep.Add((ms.ToArray(), row > delEnd ? row - count : row, pic.TopLeftCell.Address.ColumnNumber,
                                  (int)off.X, (int)off.Y, pic.Width, pic.Height));
                    }
                    pic.Delete();
                }
                ws.Rows(delStart, delEnd).Delete();
                wb.Save();
            }

            using (var wb = new XLWorkbook(work))
            {
                var ws = wb.Worksheet(sheetName);
                foreach (var k in keep)
                {
                    ws.AddPicture(new MemoryStream(k.image))
                      .MoveTo(ws.Cell(k.row, k.col), k.offX, k.offY)
                      .WithSize(k.w, k.h);
                }
                wb.Save();
            }

            File.Copy(work, xlsxPath, true);
        }
        finally
        {
            try { File.Delete(work); } catch { /* 임시 파일 정리 실패는 무시 */ }
        }
    }

    private static List<FraHistoryBlockInfo> FindBlocks(IXLWorksheet ws)
        => ws.MergedRanges
            .Where(r => r.RangeAddress.FirstAddress.ColumnNumber == FirstCol
                     && r.RangeAddress.LastAddress.ColumnNumber == FirstCol
                     && r.RangeAddress.FirstAddress.RowNumber > HeadRow)
            .Select(r =>
            {
                int s = r.RangeAddress.FirstAddress.RowNumber;
                string Get(int i) => ws.Cell(s, FirstCol + i).GetFormattedString();
                return new FraHistoryBlockInfo(s, r.RangeAddress.LastAddress.RowNumber,
                    new FraHistoryHeader(Get(0), Get(1), Get(2), Get(3), Get(4), Get(5)));
            })
            .OrderBy(b => b.StartRow)
            .ToList();

    private static void SetupSheet(IXLWorksheet ws, string sheetName, FraHistorySpec spec)
    {
        ws.Style.Font.FontName = "맑은 고딕";
        ws.Style.Font.FontSize = 10;
        ws.ShowGridLines = false;

        ws.Cell("B1").Value = $"{sheetName} — History";
        ws.Cell("B1").Style.Font.SetBold().Font.SetFontSize(16);
        ws.Cell("B2").Value = $"Spec (MeasureHub): GM ≥ {Fmt(spec.Gm)} dB, PM ≥ {Fmt(spec.Pm)}°, -3dB PM ≥ {Fmt(spec.Pm3)}°   |   수치·그래프 출처: MeasureHub FRA 결과";
        ws.Cell("B2").Style.Font.FontColor = XLColor.Gray;

        for (int i = 0; i < Heads.Length; i++)
        {
            ws.Cell(HeadRow, FirstCol + i).Value = Heads[i];
            ws.Column(FirstCol + i).Width = HeadWidths[i];
        }
        for (int g = 0; g < MaxGroups; g++)
        {
            ws.Cell(HeadRow, GraphCol + g).Value = $"Group {g + 1}  Bode Plot";
            ws.Column(GraphCol + g).Width = GraphColWidth;
        }
        ws.Column(LastTableCol + 1).Width = 2;

        foreach (var r in new[] { ws.Range(HeadRow, FirstCol, HeadRow, LastTableCol), ws.Range(HeadRow, GraphCol, HeadRow, GraphCol + MaxGroups - 1) })
        {
            r.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F1"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetWrapText()
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin);
        }
        ws.Row(HeadRow).Height = 32;
        ws.SheetView.FreezeRows(HeadRow);
        ws.SheetView.ZoomScale = 70;
    }

    private static void WriteBlock(IXLWorksheet ws, int startRow, FraHistoryBuild build, FraHistorySpec spec, string tempDir)
    {
        int row = startRow;
        foreach (var axisName in AxisOrder)
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

            var axisCell = ws.Range(subStart, AxisCol, subEnd, AxisCol).Merge();
            axisCell.Value = axisName;
            Center(axisCell);
            axisCell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EBF1DE"));

            ws.Range(subStart, AxisCol, subEnd, LastTableCol).Style
                .Border.SetInsideBorder(XLBorderStyleValues.Hair)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            ws.Range(subStart, GroupCol, subEnd, LastTableCol).Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            ws.Range(subStart, FileCol, subEnd, FileCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

            AddGroupGraphs(ws, subStart, subEnd, axisName, axis?.Groups ?? [], tempDir);
        }
        int blockEnd = row - 1;

        string[] info = [build.Stage, build.Maker, build.Date, build.Owner, build.Reason, build.Opinion];
        for (int i = 0; i < info.Length; i++)
        {
            var r = ws.Range(startRow, FirstCol + i, blockEnd, FirstCol + i).Merge();
            r.Value = info[i];
            Center(r);
            r.Style.Border.SetRightBorder(XLBorderStyleValues.Thin);
        }
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
        double colPx = GraphColWidth * 7 + 5;
        double subPx = ws.Rows(subStart, subEnd).Sum(r => r.Height) * 96 / 72;
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

            // 참고: XLPicturePlacement.MoveAndSize 를 주면 ClosedXML 0.104 가 만든 파일을 Excel 이 "복구"해야 열 수 있어 쓰지 않는다.
            //       그래서 Excel 에서 행을 지워도 그림은 남는다 → 빌드 삭제는 앱의 [삭제] 기능(DeleteBuild)을 쓴다.
            ws.AddPicture(png)
              .MoveTo(ws.Cell(subStart, GraphCol + g), (int)((colPx - w) / 2), (int)((subPx - h) / 2))
              .WithSize((int)w, (int)h);
        }
    }

    private static void Center(IXLRange r)
        => r.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                  .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                  .Alignment.SetWrapText();

    private static string Fmt(double? v) => v?.ToString("0.##") ?? "-";
}
