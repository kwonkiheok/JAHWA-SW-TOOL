using System.Globalization;

using System.IO;

namespace JahwaInspector.Light.Services;

public static class RobocubeScanReader
{
    public static ScanReadResult Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Empty robocube file.");

        int upHeader = FindSectionHeader(lines, "(Up)", "Target", "LaserDis");
        int downHeader = FindSectionHeader(lines, "(Down)", "Target", "LaserDis");
        if (upHeader < 0 || downHeader < 0)
            throw new InvalidOperationException("robocube Up/Down sections were not found.");

        var upHeaderCells = Split(lines[upHeader]);
        var downHeaderCells = Split(lines[downHeader]);
        int upInputIndex = FindColumn(upHeaderCells, "Target");
        int upOutputIndex = FindColumn(upHeaderCells, "LaserDis");
        int downInputIndex = FindColumn(downHeaderCells, "Target");
        int downOutputIndex = FindColumn(downHeaderCells, "LaserDis");

        if (upInputIndex < 0 || upOutputIndex < 0 || downInputIndex < 0 || downOutputIndex < 0)
            throw new InvalidOperationException("robocube Target/LaserDis columns were not found.");

        var (upInput, upOutput) = ReadSection(lines, upHeader + 1, downHeader, upInputIndex, upOutputIndex);
        var (downInput, downOutput) = ReadSection(lines, downHeader + 1, lines.Length, downInputIndex, downOutputIndex);
        if (upInput.Count < 2 || downInput.Count < 2)
            throw new InvalidOperationException("Not enough valid robocube SCAN data rows.");

        return new ScanReadResult
        {
            HeaderRow = upHeader,
            InputColumn = "Target",
            OutputColumn = "LaserDis",
            UpInput = upInput,
            UpOutput = upOutput,
            DownInput = downInput,
            DownOutput = downOutput,
            RangeHints = ReadRangeHints(lines),
        };
    }

    internal static int FindSectionHeader(IReadOnlyList<string> lines, string marker, params string[] requiredColumns)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var cells = Split(lines[i]);
            if (HeaderMatch.FindIndex(cells, [marker], allowFuzzy: false) >= 0
                && requiredColumns.All(col => FindColumn(cells, col) >= 0))
            {
                return i;
            }
        }

        return -1;
    }

    internal static (List<double> input, List<double> output) ReadSection(
        IReadOnlyList<string> lines,
        int start,
        int end,
        int inputIndex,
        int outputIndex)
    {
        var input = new List<double>();
        var output = new List<double>();

        for (int i = start; i < end; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.TrimStart().StartsWith("[", StringComparison.Ordinal))
                break;

            var cells = Split(line);
            if (cells.Count <= Math.Max(inputIndex, outputIndex))
                continue;

            if (!TryParseDouble(cells[inputIndex], out var x))
                continue;
            if (!TryParseDouble(cells[outputIndex], out var y))
                continue;

            input.Add(x);
            output.Add(y);
        }

        return (input, output);
    }

    internal static IReadOnlyList<ScanRangeHint> ReadRangeHints(IReadOnlyList<string> lines)
    {
        double? hysStart = null;
        double? hysEnd = null;

        foreach (var line in lines)
        {
            if (TryReadBracketValue(line, "Hys Start Code", out var start))
                hysStart = start;
            else if (TryReadBracketValue(line, "Hys End Code", out var end))
                hysEnd = end;
        }

        if (hysStart is double min && hysEnd is double max && Math.Abs(max - min) > 1e-12)
        {
            if (max < min)
                (min, max) = (max, min);
            return new[] { new ScanRangeHint(min, max) };
        }

        return Array.Empty<ScanRangeHint>();
    }

    private static bool TryReadBracketValue(string line, string name, out double value)
    {
        value = 0;
        var prefix = $"[{name}]";
        if (!line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var tail = line.Trim().Substring(prefix.Length).Trim();
        return TryParseDouble(tail, out value);
    }

    // 헤더 이름 변형 대응은 HeaderMatch가 담당한다.
    // 기존 Normalize는 공백만 제거해서 "LaserDis[um]" 같은 변형을 놓쳤다.
    internal static int FindColumn(IReadOnlyList<string> cells, params string[] names)
        => HeaderMatch.FindIndex(cells, names);

    internal static List<string> Split(string line)
    {
        if (line.Contains('\t'))
            return line.Split('\t').Select(x => x.Trim()).ToList();

        if (line.Contains(','))
            return line.Split(',').Select(x => x.Trim()).ToList();

        return line
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    internal static bool TryParseDouble(string? value, out double result)
        => HeaderMatch.TryParseNumber(value, out result);
}
