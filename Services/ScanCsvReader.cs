using Microsoft.VisualBasic.FileIO;

using System.IO;

namespace JahwaInspector.Light.Services;

public sealed record ScanRangeHint(double MinInput, double MaxInput);

public sealed class ScanReadResult
{
    public int HeaderRow { get; init; }
    public string InputColumn { get; init; } = "";
    public string OutputColumn { get; init; } = "";
    public IReadOnlyList<double> UpInput { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> UpOutput { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DownInput { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DownOutput { get; init; } = Array.Empty<double>();
    public IReadOnlyList<ScanRangeHint> RangeHints { get; init; } = Array.Empty<ScanRangeHint>();
}

public static class ScanCsvReader
{
    public static ScanReadResult Read(
        string csvPath,
        IEnumerable<string> inputCandidates,
        IEnumerable<string> outputCandidates,
        int maxScanRows = 80)
    {
        var rows = ReadAllRows(csvPath);
        if (rows.Count == 0)
            throw new InvalidOperationException("Empty CSV file.");

        int headerRow = DetectHeaderRow(rows, inputCandidates, outputCandidates, maxScanRows);
        if (headerRow < 0)
            throw new InvalidOperationException("SCAN header (Input/Output) was not found.");

        var header = rows[headerRow].Select(x => (x ?? string.Empty).Trim()).ToList();
        var inputCol = FindBestMatchingColumn(header, inputCandidates);
        var outputCol = FindBestMatchingColumn(header, outputCandidates);
        if (inputCol is null || outputCol is null)
            throw new InvalidOperationException("Failed to match SCAN Input/Output columns.");

        int ix = header.FindIndex(x => string.Equals(x, inputCol, StringComparison.Ordinal));
        int iy = header.FindIndex(x => string.Equals(x, outputCol, StringComparison.Ordinal));
        if (ix < 0 || iy < 0)
            throw new InvalidOperationException("Failed to resolve SCAN column indexes.");

        var xs = new List<double>();
        var ys = new List<double>();
        for (int r = headerRow + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (ix >= row.Count || iy >= row.Count)
                continue;

            var sx = row[ix]?.Trim();
            var sy = row[iy]?.Trim();
            if (!TryParseFlexibleDouble(sx, out var x))
                continue;
            if (!TryParseFlexibleDouble(sy, out var y))
                continue;

            xs.Add(x);
            ys.Add(y);
        }

        if (xs.Count < 2)
            throw new InvalidOperationException("Not enough valid SCAN data rows.");

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

        return new ScanReadResult
        {
            HeaderRow = headerRow,
            InputColumn = inputCol,
            OutputColumn = outputCol,
            UpInput = upInput,
            UpOutput = upOutput,
            DownInput = downInput,
            DownOutput = downOutput,
            RangeHints = ParseRangeHints(rows, headerRow),
        };
    }

    private static List<List<string>> ReadAllRows(string csvPath)
    {
        var rows = new List<List<string>>();
        using var parser = new TextFieldParser(csvPath);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(DetectDelimiter(csvPath));
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? Array.Empty<string>();
            rows.Add(fields.ToList());
        }

        return rows;
    }

    private static string DetectDelimiter(string path)
    {
        foreach (var line in File.ReadLines(path).Take(20))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            int tabs = line.Count(c => c == '\t');
            int commas = line.Count(c => c == ',');
            return tabs > commas ? "\t" : ",";
        }

        return ",";
    }

    // 헤더 행 탐지도 HeaderMatch로 위임한다. Input/Output 후보가 모두 발견되는 첫 행이 헤더 행이다.
    private static int DetectHeaderRow(IReadOnlyList<List<string>> rows, IEnumerable<string> inputCandidates, IEnumerable<string> outputCandidates, int maxScanRows)
        => HeaderMatch.FindHeaderRow(rows, maxScanRows, inputCandidates, outputCandidates);

    private static IReadOnlyList<ScanRangeHint> ParseRangeHints(IReadOnlyList<List<string>> rows, int headerRow)
    {
        for (int i = 0; i < Math.Min(headerRow, rows.Count); i++)
        {
            if (rows[i].Count == 0)
                continue;

            var head = NormalizeName(rows[i][0]);
            if (!string.Equals(head, "range", StringComparison.Ordinal))
                continue;

            var result = new List<ScanRangeHint>();
            for (int c = 1; c < rows[i].Count; c++)
            {
                if (TryParseRange(rows[i][c], out var min, out var max))
                    result.Add(new ScanRangeHint(min, max));
            }

            return result;
        }

        return Array.Empty<ScanRangeHint>();
    }

    private static bool TryParseRange(string? token, out double min, out double max)
    {
        min = 0;
        max = 0;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var s = token.Trim();
        var parts = s.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!TryParseFlexibleDouble(parts[0], out min) || !TryParseFlexibleDouble(parts[1], out max))
            return false;

        if (max < min)
            (min, max) = (max, min);

        return true;
    }

    private static string? FindBestMatchingColumn(IEnumerable<string> columns, IEnumerable<string> candidates)
    {
        var map = columns.ToDictionary(NormalizeName, x => x, StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var key = NormalizeName(candidate);
            if (map.TryGetValue(key, out var actual))
                return actual;
        }

        return null;
    }

    // 정규화 규칙은 HeaderMatch 한 곳에서만 관리한다.
    private static string NormalizeName(string? name) => HeaderMatch.Normalize(name);

    private static bool TryParseFlexibleDouble(string? s, out double value)
        => HeaderMatch.TryParseNumber(s, out value);
}
