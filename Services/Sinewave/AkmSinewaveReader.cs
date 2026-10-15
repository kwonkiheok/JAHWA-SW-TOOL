using System.Globalization;
using System.IO;

namespace MeasureHub.Services;

public sealed class SinewaveReadResult
{
    public int HeaderRow { get; init; }
    public IReadOnlyList<double> TimeMs { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> PosiX { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> HallX { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DiffX { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> PosiY { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> HallY { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DiffY { get; init; } = Array.Empty<double>();
}

public static class AkmSinewaveReader
{
    public static SinewaveReadResult Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Empty AKM Sinewave file.");

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
        // 목표값 열은 Posi_* 가 기본이고, 일부 AKM 로그(예: 10840_SineWave_L01)는 ReadVt_* 로 기록한다(Circle Test 와 같은 이름).
        int posiXIndex = FindColumn(headerCells, "Posi_X", "ReadVt_X");
        int hallXIndex = FindColumn(headerCells, "Hall_X");
        int diffXIndex = FindColumn(headerCells, "Diff_X");
        int posiYIndex = FindColumn(headerCells, "Posi_Y", "ReadVt_Y");
        int hallYIndex = FindColumn(headerCells, "Hall_Y");
        int diffYIndex = FindColumn(headerCells, "Diff_Y");

        if (timeIndex < 0 || posiXIndex < 0 || hallXIndex < 0 || diffXIndex < 0)
            throw new InvalidOperationException("time[ms]/Posi_X(ReadVt_X)/Hall_X/Diff_X columns were not found.");
        if (posiYIndex < 0 || hallYIndex < 0 || diffYIndex < 0)
            throw new InvalidOperationException("Posi_Y(ReadVt_Y)/Hall_Y/Diff_Y columns were not found.");

        var time = new List<double>();
        var posiX = new List<double>();
        var hallX = new List<double>();
        var diffX = new List<double>();
        var posiY = new List<double>();
        var hallY = new List<double>();
        var diffY = new List<double>();

        for (int i = headerRow + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = line.Split(',');
            int maxIndex = new[] { timeIndex, posiXIndex, hallXIndex, diffXIndex, posiYIndex, hallYIndex, diffYIndex }.Max();
            if (cells.Length <= maxIndex)
                continue;

            if (!TryParseDouble(cells[timeIndex], out var t)
                || !TryParseDouble(cells[posiXIndex], out var px)
                || !TryParseDouble(cells[hallXIndex], out var hx)
                || !TryParseDouble(cells[diffXIndex], out var dx)
                || !TryParseDouble(cells[posiYIndex], out var py)
                || !TryParseDouble(cells[hallYIndex], out var hy)
                || !TryParseDouble(cells[diffYIndex], out var dy))
                continue;

            time.Add(t);
            posiX.Add(px);
            hallX.Add(hx);
            diffX.Add(dx);
            posiY.Add(py);
            hallY.Add(hy);
            diffY.Add(dy);
        }

        if (time.Count < 2)
            throw new InvalidOperationException("Not enough valid Sinewave data rows.");

        return new SinewaveReadResult
        {
            HeaderRow = headerRow,
            TimeMs = time,
            PosiX = posiX,
            HallX = hallX,
            DiffX = diffX,
            PosiY = posiY,
            HallY = hallY,
            DiffY = diffY,
        };
    }

    // 헤더 이름 변형 대응은 HeaderMatch가 담당한다.
    // (정확 일치 → 접두 일치 → 부분 일치. "Hall_X[filter set]" 같은 변형도 여기서 흡수된다.)
    private static int FindColumn(string[] headerCells, params string[] names)
        => HeaderMatch.FindIndex(headerCells, names);

    private static bool TryParseDouble(string text, out double value)
        => HeaderMatch.TryParseNumber(text, out value);
}
