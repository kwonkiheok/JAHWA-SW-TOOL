using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace JahwaInspector.Light.Services;

// DW Square 로그: preamble(Settling Tolerance Max/Min 등) 후 ",Target,X Position,Y Position" 헤더.
// Target이 반복적으로 바뀌는 사각파이며, X/Y 두 축이 한 파일에 들어있다(SINE WAVE DW와 동일한 구조).
public static class DwSquareReader
{
    public static SquareReadResult Read(string path)
    {
        var rows = ReadAllRows(path);
        if (rows.Count == 0)
            throw new InvalidOperationException("Empty DW Square file.");

        // 필요한 컬럼 3개가 모두 있는 행을 헤더 행으로 본다. "Target" 하나만 앵커로 쓰면
        // "Target(12bit)" 같은 변형에서 행을 못 찾는다. 헤더 이름 변형 대응은 HeaderMatch가 담당한다.
        int headerRow = HeaderMatch.FindHeaderRow(
            rows, rows.Count,
            ["Target"], ["X Position"], ["Y Position"]);

        if (headerRow < 0)
            throw new InvalidOperationException("DW Square header (Target/X Position/Y Position) was not found.");

        var header = rows[headerRow].Select(x => (x ?? "").Trim()).ToList();
        int targetCol = HeaderMatch.FindIndex(header, ["Target"]);
        int xCol = HeaderMatch.FindIndex(header, ["X Position"]);
        int yCol = HeaderMatch.FindIndex(header, ["Y Position"]);

        if (targetCol < 0 || xCol < 0 || yCol < 0)
            throw new InvalidOperationException("Failed to match DW Square Target/X/Y columns.");

        var time = new List<double>();
        var target = new List<double>();
        var xs = new List<double>();
        var ys = new List<double>();

        int maxIndex = new[] { targetCol, xCol, yCol }.Max();
        for (int r = headerRow + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count <= maxIndex) continue;

            if (!TryParseDouble(row[0], out var t)) continue;
            if (!TryParseDouble(row[targetCol], out var tv)) continue;
            if (!TryParseDouble(row[xCol], out var xv)) continue;
            if (!TryParseDouble(row[yCol], out var yv)) continue;

            time.Add(t);
            target.Add(tv);
            xs.Add(xv);
            ys.Add(yv);
        }

        if (time.Count < 2)
            throw new InvalidOperationException("Not enough valid DW Square data rows.");

        var axes = new[]
        {
            new SquareAxisSeries("X", time, target, xs),
            new SquareAxisSeries("Y", time, target, ys),
        };

        return new SquareReadResult(headerRow, axes);
    }

    private static List<List<string>> ReadAllRows(string csvPath)
    {
        var rows = new List<List<string>>();
        using var parser = new TextFieldParser(csvPath);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? Array.Empty<string>();
            rows.Add(fields.ToList());
        }

        return rows;
    }

    private static bool TryParseDouble(string? s, out double value)
    {
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }
}
