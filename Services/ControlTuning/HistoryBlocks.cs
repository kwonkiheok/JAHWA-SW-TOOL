using System.IO;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace MeasureHub.Services;

/// <summary>
/// Control Tuning History 시트 공통 처리 (FRA / DC 등 검사 항목과 무관한 부분).
/// 모든 History 시트는 같은 뼈대를 쓴다:
///   1~2행 제목, 4행 머리글, 5행부터 빌드 블록.
///   블록 = 빌드 1회. B~G 열 헤더(개발 단계 ~ 검토 의견)가 블록 전체에 세로 병합되고, H 열은 축(OIS X / OIS Y / AF).
/// 블록은 B열 세로 병합 범위로 찾는다. 별도 표시를 남기지 않으므로 이전에 만든 문서에도 그대로 쓸 수 있다.
/// </summary>
public static class HistoryBlocks
{
    public static readonly string[] AxisOrder = ["OIS X", "OIS Y", "AF"];

    public const int HeadRow = 4;
    public const int FirstCol = 2;                  // B열
    public const int HeaderCount = 6;               // 개발 단계 ~ 검토 의견
    public const int AxisCol = FirstCol + HeaderCount;

    public static readonly string[] HeaderHeads = ["개발 단계", "제작처", "제작일자", "검토 담당자", "검토 사유", "검토 의견"];
    public static readonly double[] HeaderWidths = [10, 8, 11, 11, 24, 24];
    public const double AxisWidth = 7;

    /// <summary>
    /// xlsxPath 의 sheetName 시트 맨 아래에 블록을 추가한다. 파일이나 시트가 없으면 새로 만든다.
    /// setupNewSheet 는 시트를 새로 만들 때만, writeBlock(ws, startRow) 은 블록을 쓸 때 호출된다.
    /// </summary>
    public static void Append(string xlsxPath, string sheetName, Action<IXLWorksheet> setupNewSheet, Action<IXLWorksheet, int> writeBlock)
    {
        using var wb = File.Exists(xlsxPath) ? new XLWorkbook(xlsxPath) : new XLWorkbook();

        if (!wb.Worksheets.TryGetWorksheet(sheetName, out var ws))
        {
            ws = wb.Worksheets.Add(sheetName);
            setupNewSheet(ws);
        }

        int startRow = (ws.LastRowUsed()?.RowNumber() ?? HeadRow) + 2;
        if (startRow <= HeadRow + 2) startRow = HeadRow + 1;   // 첫 블록은 머리글 바로 아래부터
        writeBlock(ws, startRow);

        // 새 통합문서에 기본으로 생기는 빈 시트가 있으면 지운다.
        if (wb.Worksheets.Count > 1 && wb.Worksheets.TryGetWorksheet("Sheet1", out var empty) && empty.LastCellUsed() is null)
            empty.Delete();

        if (File.Exists(xlsxPath)) wb.Save();
        else wb.SaveAs(xlsxPath);
    }

    /// <summary>새 시트의 공통 서식 + 제목 + 머리글(B~H). 검사별 머리글은 호출한 쪽에서 이어서 쓴다.</summary>
    public static void SetupSheet(IXLWorksheet ws, string title, string subtitle)
    {
        ws.Style.Font.FontName = "맑은 고딕";
        ws.Style.Font.FontSize = 10;
        ws.ShowGridLines = false;

        ws.Cell("B1").Value = title;
        ws.Cell("B1").Style.Font.SetBold().Font.SetFontSize(16);
        ws.Cell("B2").Value = subtitle;
        ws.Cell("B2").Style.Font.FontColor = XLColor.Gray;

        for (int i = 0; i < HeaderCount; i++)
        {
            ws.Cell(HeadRow, FirstCol + i).Value = HeaderHeads[i];
            ws.Column(FirstCol + i).Width = HeaderWidths[i];
        }
        ws.Cell(HeadRow, AxisCol).Value = "Axis";
        ws.Column(AxisCol).Width = AxisWidth;

        ws.Row(HeadRow).Height = 32;
        ws.SheetView.FreezeRows(HeadRow);
        ws.SheetView.ZoomScale = 70;
    }

    public static void StyleHeadRange(IXLRange r)
        => r.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F1"))
               .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
               .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
               .Alignment.SetWrapText()
               .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
               .Border.SetInsideBorder(XLBorderStyleValues.Thin);

    /// <summary>블록 헤더 B~G 를 블록 높이만큼 세로 병합해서 쓴다.</summary>
    public static void WriteHeader(IXLWorksheet ws, int startRow, int endRow, HistoryHeader header)
    {
        string[] info = [header.Stage, header.Maker, header.Date, header.Owner, header.Reason, header.Opinion];
        for (int i = 0; i < info.Length; i++)
        {
            var r = ws.Range(startRow, FirstCol + i, endRow, FirstCol + i).Merge();
            r.Value = info[i];
            Center(r);
            r.Style.Border.SetRightBorder(XLBorderStyleValues.Thin);
        }
    }

    /// <summary>축 라벨 칸(H열)을 축 행 높이만큼 병합해서 쓴다.</summary>
    public static void WriteAxisLabel(IXLWorksheet ws, int subStart, int subEnd, string axis)
    {
        var r = ws.Range(subStart, AxisCol, subEnd, AxisCol).Merge();
        r.Value = axis;
        Center(r);
        r.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EBF1DE"))
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
    }

    /// <summary>
    /// 칸(병합 영역) 안에 PNG 를 가운데 맞춰 넣는다. 크기는 px.
    /// 참고: XLPicturePlacement.MoveAndSize 를 주면 ClosedXML 0.104 가 만든 파일을 Excel 이 "복구"해야 열 수 있어 쓰지 않는다.
    ///       그래서 Excel 에서 행을 지워도 그림은 남는다 → 빌드 삭제는 앱의 [삭제] 기능(DeleteBuild)을 쓴다.
    /// </summary>
    public static void AddPicture(IXLWorksheet ws, string png, int row, int col, double areaW, double areaH, double w, double h)
        => ws.AddPicture(png)
             .MoveTo(ws.Cell(row, col), (int)((areaW - w) / 2), (int)((areaH - h) / 2))
             .WithSize((int)w, (int)h);

    /// <summary>열 너비(문자 단위) → px (맑은 고딕 10pt 기준 근사).</summary>
    public static double ColumnPx(double width) => width * 7 + 5;

    /// <summary>행 높이 합(pt) → px.</summary>
    public static double RowsPx(IXLWorksheet ws, int fromRow, int toRow)
        => ws.Rows(fromRow, toRow).Sum(r => r.Height) * 96 / 72;

    public static void Center(IXLRange r)
        => r.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                  .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                  .Alignment.SetWrapText();

    // ---------------- 기존 빌드 목록 / 헤더 편집 / 삭제 ----------------

    public static IReadOnlyList<HistoryBlockInfo> ListBuilds(string xlsxPath, string sheetName)
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
    public static void UpdateHeader(string xlsxPath, string sheetName, int startRow, HistoryHeader header)
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

    private static List<HistoryBlockInfo> FindBlocks(IXLWorksheet ws)
        => ws.MergedRanges
            .Where(r => r.RangeAddress.FirstAddress.ColumnNumber == FirstCol
                     && r.RangeAddress.LastAddress.ColumnNumber == FirstCol
                     && r.RangeAddress.FirstAddress.RowNumber > HeadRow)
            .Select(r =>
            {
                int s = r.RangeAddress.FirstAddress.RowNumber;
                string Get(int i) => ws.Cell(s, FirstCol + i).GetFormattedString();
                return new HistoryBlockInfo(s, r.RangeAddress.LastAddress.RowNumber,
                    new HistoryHeader(Get(0), Get(1), Get(2), Get(3), Get(4), Get(5)));
            })
            .OrderBy(b => b.StartRow)
            .ToList();
}
