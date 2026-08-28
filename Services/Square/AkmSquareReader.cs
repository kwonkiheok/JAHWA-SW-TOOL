using System.Globalization;
using System.IO;

namespace JahwaInspector.Light.Services;

// AKM Square 로그: 헤더 메타데이터(amplitude, tolerance(%amp))에 이어
// "time[ms],pos,read_vt,hall,read_mv" 시계열. pos(목표)가 중심↔±amplitude를 반복하는 사각파.
// 파일당 축 1개(파일명에 X/Y 표기는 있으나 컬럼 자체는 공통).
public static class AkmSquareReader
{
    public static SquareReadResult Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Empty AKM Square file.");

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
        int posIndex = FindColumn(headerCells, "pos");
        int hallIndex = FindColumn(headerCells, "hall");

        if (timeIndex < 0 || posIndex < 0 || hallIndex < 0)
            throw new InvalidOperationException("time[ms]/pos/hall columns were not found.");

        var time = new List<double>();
        var target = new List<double>();
        var value = new List<double>();

        int maxIndex = new[] { timeIndex, posIndex, hallIndex }.Max();
        for (int i = headerRow + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = line.Split(',');
            if (cells.Length <= maxIndex)
                continue;

            if (!TryParseDouble(cells[timeIndex], out var t)
                || !TryParseDouble(cells[posIndex], out var p)
                || !TryParseDouble(cells[hallIndex], out var h))
                continue;

            time.Add(t);
            target.Add(p);
            value.Add(h);
        }

        if (time.Count < 2)
            throw new InvalidOperationException("Not enough valid Square data rows.");

        var axis = new SquareAxisSeries("Value", time, target, value);
        return new SquareReadResult(headerRow, new[] { axis });
    }

    // 헤더 이름 변형 대응은 HeaderMatch가 담당한다.
    // (정확 일치 -> 접두 일치 -> 부분 일치. 여러 컬럼이 걸리면 모호하므로 채택하지 않는다.)
    private static int FindColumn(string[] headerCells, params string[] names)
        => HeaderMatch.FindIndex(headerCells, names);

    private static bool TryParseDouble(string text, out double value)
        => HeaderMatch.TryParseNumber(text, out value);
}
