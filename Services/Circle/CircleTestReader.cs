using System.Globalization;
using System.IO;

namespace JahwaInspector.Light.Services;

public sealed class CircleReadResult
{
    public int HeaderRow { get; init; }
    public IReadOnlyList<double> TimeMs { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> ReadVtX { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> HallX { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DiffX { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> ReadVtY { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> HallY { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DiffY { get; init; } = Array.Empty<double>();
}

// AKM CircleTest 로그: time[ms], ReadVt_X, Hall_X, Diff_X, ReadVt_Y, Hall_Y, Diff_Y.
// SINE WAVE의 AkmSinewaveReader와 동일한 구조지만 컬럼명이 Posi_* 대신 ReadVt_*.
public static class CircleTestReader
{
    public static CircleReadResult Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Empty CircleTest file.");

        int headerRow = -1;
        string[] headerCells = Array.Empty<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            var cells = lines[i].Split(',');
            // 헤더 행 앵커는 정확 일치로만 찾는다. 부분 일치를 허용하면 전문(preamble)의
            // "DateTime" 행이 "time"에 걸려 헤더 행으로 오검출된다.
            if (HeaderMatch.FindIndex(cells, ["time[ms]"], allowFuzzy: false) >= 0)
            {
                headerRow = i;
                headerCells = cells;
                break;
            }
        }

        if (headerRow < 0)
            throw new InvalidOperationException("'time[ms]' header row was not found.");

        int timeIndex = FindColumn(headerCells, "time[ms]");
        int readVtXIndex = FindColumn(headerCells, "ReadVt_X");
        int hallXIndex = FindColumn(headerCells, "Hall_X");
        int diffXIndex = FindColumn(headerCells, "Diff_X");
        int readVtYIndex = FindColumn(headerCells, "ReadVt_Y");
        int hallYIndex = FindColumn(headerCells, "Hall_Y");
        int diffYIndex = FindColumn(headerCells, "Diff_Y");

        if (timeIndex < 0 || readVtXIndex < 0 || hallXIndex < 0 || diffXIndex < 0)
            throw new InvalidOperationException("time[ms]/ReadVt_X/Hall_X/Diff_X columns were not found.");
        if (readVtYIndex < 0 || hallYIndex < 0 || diffYIndex < 0)
            throw new InvalidOperationException("ReadVt_Y/Hall_Y/Diff_Y columns were not found.");

        var time = new List<double>();
        var readVtX = new List<double>();
        var hallX = new List<double>();
        var diffX = new List<double>();
        var readVtY = new List<double>();
        var hallY = new List<double>();
        var diffY = new List<double>();

        int maxIndex = new[] { timeIndex, readVtXIndex, hallXIndex, diffXIndex, readVtYIndex, hallYIndex, diffYIndex }.Max();
        for (int i = headerRow + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = line.Split(',');
            if (cells.Length <= maxIndex)
                continue;

            if (!TryParseDouble(cells[timeIndex], out var t)
                || !TryParseDouble(cells[readVtXIndex], out var rvx)
                || !TryParseDouble(cells[hallXIndex], out var hx)
                || !TryParseDouble(cells[diffXIndex], out var dx)
                || !TryParseDouble(cells[readVtYIndex], out var rvy)
                || !TryParseDouble(cells[hallYIndex], out var hy)
                || !TryParseDouble(cells[diffYIndex], out var dy))
                continue;

            time.Add(t);
            readVtX.Add(rvx);
            hallX.Add(hx);
            diffX.Add(dx);
            readVtY.Add(rvy);
            hallY.Add(hy);
            diffY.Add(dy);
        }

        if (time.Count < 2)
            throw new InvalidOperationException("Not enough valid CircleTest data rows.");

        return new CircleReadResult
        {
            HeaderRow = headerRow,
            TimeMs = time,
            ReadVtX = readVtX,
            HallX = hallX,
            DiffX = diffX,
            ReadVtY = readVtY,
            HallY = hallY,
            DiffY = diffY,
        };
    }

    // 헤더 이름 변형 대응은 HeaderMatch가 담당한다.
    // (정확 일치 -> 접두 일치 -> 부분 일치. 여러 컬럼이 걸리면 모호하므로 채택하지 않는다.)
    private static int FindColumn(string[] headerCells, params string[] names)
        => HeaderMatch.FindIndex(headerCells, names);

    private static bool TryParseDouble(string text, out double value)
        => HeaderMatch.TryParseNumber(text, out value);
}
