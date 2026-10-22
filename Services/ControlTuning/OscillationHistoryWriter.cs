using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ClosedXML.Excel;

namespace MeasureHub.Services;

/// <summary>
/// 발진 History 시트에 빌드 블록을 하나씩 쌓는다. (공통 뼈대는 <see cref="HistoryBlocks"/>, 양식 Test_발진 과 같은 격자)
/// 계산 없이, 발진 시험 후 저장한 파형 사진을 격자로 넣는다.
///   행 = 측정 조건(H 열) → 그룹(Cav1~4, I 열)마다 새 줄, 한 줄에 Columns 장, 넘치면 같은 그룹 안에서 다음 줄
///   한 줄 = 칸 제목 행(SPL n) + 사진 행. 사진 행 높이와 사진 열 너비는 사용자가 정한 칸 크기(cm)에 맞춘다.
///   사진은 칸 가운데에 넣고, KeepAspect 면 가로×세로 상자 안에 원본 비율로 맞춘다.
///   문서 용량을 줄이려고 사진은 표시 크기의 2배 해상도로 줄여서 넣는다(원본이 더 작으면 그대로).
/// 열 너비는 시트의 모든 빌드가 함께 쓰므로, 더 큰 칸이 필요할 때만 넓힌다(좁히지 않는다).
/// </summary>
public static class OscillationHistoryWriter
{
    public const int MaxConditions = 4;
    public const int MaxColumns = 12;

    private const int FirstCol = HistoryBlocks.FirstCol;
    private const int GroupCol = HistoryBlocks.AxisCol + 1;
    private const int FirstPhotoCol = GroupCol + 1;

    private const double TitleRowHeight = 18;       // pt
    private const double PxPerCm = 96 / 2.54;        // 화면 96dpi 기준
    private const double PtPerCm = 72 / 2.54;
    private const double MarginCm = 0.3;              // 칸 안 여백(위아래·좌우 합)
    private const double StoreScale = 2.0;            // 표시 크기 대비 저장 해상도

    private const string PhotoHead = "파형 사진";

    public static void AppendBuild(string xlsxPath, string sheetName, OscillationBuild build, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        HistoryBlocks.Append(xlsxPath, sheetName,
            ws => SetupSheet(ws, sheetName),
            (ws, startRow) => WriteBlock(ws, startRow, build, tempDir));
    }

    private static void SetupSheet(IXLWorksheet ws, string sheetName)
    {
        HistoryBlocks.SetupSheet(ws, $"{sheetName} — History",
            "행 = 측정 조건 → 그룹(Cav)   |   칸 = 발진 시험 파형 사진 (칸 제목: SPL 번호)   |   출처: 측정 장비 파형 캡처");
        ws.Cell(HistoryBlocks.HeadRow, HistoryBlocks.AxisCol).Value = "조건";
        ws.Column(HistoryBlocks.AxisCol).Width = 14;
        ws.Cell(HistoryBlocks.HeadRow, GroupCol).Value = "Group";
        ws.Column(GroupCol).Width = 9;
        ws.Cell(HistoryBlocks.HeadRow, FirstPhotoCol).Value = PhotoHead;
        HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, FirstCol, HistoryBlocks.HeadRow, GroupCol));
    }

    private static void WriteBlock(IXLWorksheet ws, int startRow, OscillationBuild build, string tempDir)
    {
        EnsureHeadsMatch(ws);
        var layout = build.Layout;
        int cols = Math.Clamp(layout.Columns, 1, MaxColumns);
        int lastPhotoCol = FirstPhotoCol + cols - 1;

        // 칸 크기(cm) → 열 너비(문자)·행 높이(pt). 열은 넓히기만 한다.
        double cellWcm = layout.WidthCm + MarginCm, cellHcm = layout.HeightCm + MarginCm;
        double colWidth = (cellWcm * PxPerCm - 5) / 7;
        for (int c = FirstPhotoCol; c <= lastPhotoCol; c++)
            if (ws.Column(c).Width < colWidth) ws.Column(c).Width = colWidth;
        StylePhotoHead(ws, lastPhotoCol);

        int row = startRow;
        foreach (var cond in build.Conditions.Take(MaxConditions))
        {
            int condStart = row;
            foreach (var (groupName, photos) in cond.Groups)
            {
                int groupStart = row;
                for (int i = 0; i < photos.Count; i += cols)
                {
                    var line = photos.Skip(i).Take(cols).ToList();
                    int titleRow = row, photoRow = row + 1;
                    ws.Row(titleRow).Height = TitleRowHeight;
                    ws.Row(photoRow).Height = cellHcm * PtPerCm;
                    for (int k = 0; k < cols; k++)
                    {
                        int c = FirstPhotoCol + k;
                        var title = ws.Cell(titleRow, c);
                        title.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2")).Font.SetBold()
                            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                        ws.Range(titleRow, c, photoRow, c).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                        if (k >= line.Count) continue;
                        title.Value = line[k].Label;
                        AddPhoto(ws, photoRow, c, line[k].Path, layout, tempDir);
                    }
                    row += 2;
                }
                if (row == groupStart) continue;   // 사진 없는 그룹
                var g = ws.Range(groupStart, GroupCol, row - 1, GroupCol).Merge();
                g.Value = groupName;
                HistoryBlocks.Center(g);
                g.Style.Font.SetBold().Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            }
            if (row == condStart) continue;
            HistoryBlocks.WriteAxisLabel(ws, condStart, row - 1, cond.Name);
            ws.Range(condStart, HistoryBlocks.AxisCol, row - 1, lastPhotoCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }
        int blockEnd = row - 1;
        if (blockEnd < startRow)
            throw new InvalidOperationException("문서에 넣을 사진이 없습니다.");

        HistoryBlocks.WriteHeader(ws, startRow, blockEnd, build.Header);
        ws.Range(startRow, FirstCol, blockEnd, lastPhotoCol).Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
    }

    /// <summary>머리글 "파형 사진" 칸을 이번 열 수까지 칠한다(앞 빌드가 더 많은 열을 썼으면 그 칸은 그대로 남는다).</summary>
    private static void StylePhotoHead(IXLWorksheet ws, int lastPhotoCol)
        => HistoryBlocks.StyleHeadRange(ws.Range(HistoryBlocks.HeadRow, FirstPhotoCol, HistoryBlocks.HeadRow, lastPhotoCol));

    /// <summary>헤더 행은 시트를 처음 만들 때만 쓰므로, 다른 검사 시트(열 구성이 다른 시트)에는 쓰지 않고 멈춘다.</summary>
    private static void EnsureHeadsMatch(IXLWorksheet ws)
    {
        var expected = new[] { (HistoryBlocks.AxisCol, "조건"), (GroupCol, "Group"), (FirstPhotoCol, PhotoHead) };
        foreach (var (col, text) in expected)
        {
            string actual = ws.Cell(HistoryBlocks.HeadRow, col).GetString();
            if (actual != text)
                throw new InvalidOperationException(
                    $"'{ws.Name}' 시트의 열 구성이 발진 형식과 다릅니다(다른 검사 시트).\n" +
                    $"칸이 어긋나므로 추가하지 않았습니다. 다른 시트 이름 / 새 문서를 지정한 뒤 다시 추가하세요.\n\n" +
                    $"{HistoryBlocks.HeadRow}행 {ws.Cell(HistoryBlocks.HeadRow, col).Address}: 기대 '{text}', 실제 '{actual.Replace("\n", " ")}'");
        }
    }

    /// <summary>사진을 칸 가운데에 넣는다. 저장 해상도는 표시 크기의 2배로 줄인다(원본이 더 작으면 그대로).</summary>
    private static void AddPhoto(IXLWorksheet ws, int row, int col, string path, OscillationLayout layout, string tempDir)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"사진 파일을 찾을 수 없습니다: {path}");

        double boxW = layout.WidthCm * PxPerCm, boxH = layout.HeightCm * PxPerCm;
        string stored;
        double w, h;
        using (var src = Image.FromFile(path))
        {
            (w, h) = (boxW, boxH);
            if (layout.KeepAspect)
            {
                double s = Math.Min(boxW / src.Width, boxH / src.Height);
                (w, h) = (src.Width * s, src.Height * s);
            }
            stored = Shrink(src, path, w * StoreScale, h * StoreScale, tempDir);
        }

        double areaW = HistoryBlocks.ColumnPx(ws.Column(col).Width);
        double areaH = ws.Row(row).Height * 96 / 72;
        HistoryBlocks.AddPicture(ws, stored, row, col, areaW, areaH, w, h);
    }

    private static string Shrink(Image src, string path, double maxW, double maxH, string tempDir)
    {
        if (src.Width <= maxW && src.Height <= maxH)
            return path;
        int nw = Math.Max(1, (int)Math.Round(maxW)), nh = Math.Max(1, (int)Math.Round(maxH));
        bool jpeg = Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg";
        var outPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}{(jpeg ? ".jpg" : ".png")}");
        using var bmp = new Bitmap(nw, nh);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, nw, nh);
        }
        if (jpeg)
        {
            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
            bmp.Save(outPath, codec, p);
        }
        else
        {
            bmp.Save(outPath, ImageFormat.Png);
        }
        return outPath;
    }
}
