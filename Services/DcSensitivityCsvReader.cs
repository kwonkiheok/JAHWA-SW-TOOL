using System.Data;
using Microsoft.VisualBasic.FileIO;

namespace JahwaInspector.Light.Services;

public sealed class DcSensitivityReadResult
{
    public int HeaderRow { get; init; }
    public string InputColumn { get; init; } = "";
    public string OutputColumn { get; init; } = "";
    public IReadOnlyList<double> UpInput { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> UpOutput { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DownInput { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DownOutput { get; init; } = Array.Empty<double>();
}

public static class DcSensitivityCsvReader
{
    public static DcSensitivityReadResult Read(
        string csvPath,
        IEnumerable<string> inputCandidates,
        IEnumerable<string> outputCandidates,
        int maxScanRows = 30)
    {
        var rows = ReadAllRows(csvPath);
        if (rows.Count == 0)
            throw new InvalidOperationException("빈 CSV 파일입니다.");

        var headerRow = DetectHeaderRow(rows, inputCandidates, outputCandidates, maxScanRows);
        if (headerRow < 0)
            throw new InvalidOperationException("실제 헤더 행을 찾지 못했습니다.");

        var header = rows[headerRow].Select(x => (x ?? string.Empty).Trim()).ToList();
        var inputCol = FindBestMatchingColumn(header, inputCandidates);
        var outputCol = FindBestMatchingColumn(header, outputCandidates);

        if (inputCol is null)
            throw new InvalidOperationException("Input 컬럼 매칭 실패");
        if (outputCol is null)
            throw new InvalidOperationException("Output 컬럼 매칭 실패");

        int ix = header.FindIndex(x => string.Equals(x, inputCol, StringComparison.Ordinal));
        int iy = header.FindIndex(x => string.Equals(x, outputCol, StringComparison.Ordinal));
        if (ix < 0 || iy < 0)
            throw new InvalidOperationException("매칭된 컬럼 인덱스를 찾지 못했습니다.");

        var xs = new List<double>();
        var ys = new List<double>();

        for (int r = headerRow + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (ix >= row.Count || iy >= row.Count) continue;

            var sx = row[ix]?.Trim();
            var sy = row[iy]?.Trim();
            if (!TryParseFlexibleDouble(sx, out var x)) continue;
            if (!TryParseFlexibleDouble(sy, out var y)) continue;

            xs.Add(x);
            ys.Add(y);
        }

        if (xs.Count == 0)
            throw new InvalidOperationException("유효한 숫자 데이터가 없습니다.");

        int turnIdx = 0;
        double maxX = xs[0];
        for (int i = 1; i < xs.Count; i++)
        {
            if (xs[i] > maxX)
            {
                maxX = xs[i];
                turnIdx = i;
            }
        }

        var upInput = xs.Take(turnIdx + 1).ToList();
        var upOutput = ys.Take(turnIdx + 1).ToList();
        var downInput = xs.Skip(turnIdx + 1).ToList();
        var downOutput = ys.Skip(turnIdx + 1).ToList();

        return new DcSensitivityReadResult
        {
            HeaderRow = headerRow,
            InputColumn = inputCol,
            OutputColumn = outputCol,
            UpInput = upInput,
            UpOutput = upOutput,
            DownInput = downInput,
            DownOutput = downOutput,
        };
    }

    public static DataTable BuildCombinedOutputOnlyTable(IReadOnlyList<(string FileStem, IReadOnlyList<double> UpOutput, IReadOnlyList<double> DownOutput)> items)
    {
        var dt = new DataTable();
        foreach (var item in items)
        {
            dt.Columns.Add($"{item.FileStem}_up", typeof(double));
            dt.Columns.Add($"{item.FileStem}_down", typeof(double));
        }

        int maxRows = items.Count == 0 ? 0 : items.Max(x => Math.Max(x.UpOutput.Count, x.DownOutput.Count));
        for (int r = 0; r < maxRows; r++)
        {
            var row = dt.NewRow();
            int c = 0;
            foreach (var item in items)
            {
                row[c++] = r < item.UpOutput.Count ? item.UpOutput[r] : DBNull.Value;
                int revDownIdx = item.DownOutput.Count - 1 - r;
                row[c++] = (revDownIdx >= 0 && revDownIdx < item.DownOutput.Count) ? item.DownOutput[revDownIdx] : DBNull.Value;
            }
            dt.Rows.Add(row);
        }

        return dt;
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

    // 헤더 행 탐지도 HeaderMatch로 위임한다. Input/Output 후보가 모두 발견되는 첫 행이 헤더 행이다.
    private static int DetectHeaderRow(List<List<string>> rows, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates, int maxScanRows)
        => HeaderMatch.FindHeaderRow(rows, maxScanRows, inputCandidates, outputCandidates);

    // 헤더 이름 변형 대응(정확 -> 접두 -> 부분 일치)은 HeaderMatch가 담당한다.
    private static string? FindBestMatchingColumn(IEnumerable<string> columns, IEnumerable<string> candidates)
        => HeaderMatch.FindName(columns, candidates);

    // 정규화 규칙은 HeaderMatch 한 곳에서만 관리한다.
    private static string NormalizeName(string? name) => HeaderMatch.Normalize(name);

    private static bool TryParseFlexibleDouble(string? s, out double value)
        => HeaderMatch.TryParseNumber(s, out value);
}
