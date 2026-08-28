using System.Globalization;
using System.IO;

namespace JahwaInspector.Light.Services;

// DW의 신형 Sine Wave 로그 포맷: 시간, Target/Feedback/Position(12bit) 컬럼을 X/Y 모두 한 줄에 기록.
// Diff는 파일에 없으므로 사용자 확정에 따라 Target 12bit - Position 12bit로 직접 계산한다.
public static class DwSinewaveReader
{
    public static SinewaveReadResult Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Empty DW Sinewave file.");

        int headerRow = -1;
        string[] headerCells = Array.Empty<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            var cells = lines[i].Split(',');
            // 필요한 컬럼 4개가 모두 있는 행을 헤더 행으로 본다.
            // 앵커 1개만 보면 "X Target 12bit(raw)" 같은 변형에서 행을 못 찾고,
            // 반대로 느슨한 후보 1개만 보면 전문(preamble) 행이 잘못 걸린다.
            if (HeaderMatch.FindIndex(cells, ["X Target 12bit"]) >= 0
                && HeaderMatch.FindIndex(cells, ["X Position 12bit"]) >= 0
                && HeaderMatch.FindIndex(cells, ["Y Target 12bit"]) >= 0
                && HeaderMatch.FindIndex(cells, ["Y Position 12bit"]) >= 0)
            {
                headerRow = i;
                headerCells = cells;
                break;
            }
        }

        if (headerRow < 0)
            throw new InvalidOperationException("'X Target 12bit' header row was not found.");

        int targetXIndex = FindColumn(headerCells, "X Target 12bit");
        int positionXIndex = FindColumn(headerCells, "X Position 12bit");
        int targetYIndex = FindColumn(headerCells, "Y Target 12bit");
        int positionYIndex = FindColumn(headerCells, "Y Position 12bit");

        if (targetXIndex < 0 || positionXIndex < 0 || targetYIndex < 0 || positionYIndex < 0)
            throw new InvalidOperationException("X/Y Target 12bit, X/Y Position 12bit columns were not found.");

        var time = new List<double>();
        var posiX = new List<double>();
        var hallX = new List<double>();
        var diffX = new List<double>();
        var posiY = new List<double>();
        var hallY = new List<double>();
        var diffY = new List<double>();

        int maxIndex = new[] { targetXIndex, positionXIndex, targetYIndex, positionYIndex }.Max();
        double t = 0;
        for (int i = headerRow + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = line.Split(',');
            if (cells.Length <= maxIndex)
                continue;

            if (!TryParseDouble(cells[targetXIndex], out var tx)
                || !TryParseDouble(cells[positionXIndex], out var px)
                || !TryParseDouble(cells[targetYIndex], out var ty)
                || !TryParseDouble(cells[positionYIndex], out var py))
                continue;

            time.Add(t);
            posiX.Add(tx);
            hallX.Add(px);
            diffX.Add(tx - px);
            posiY.Add(ty);
            hallY.Add(py);
            diffY.Add(ty - py);
            t += 1;
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
    // (정확 일치 -> 접두 일치 -> 부분 일치. 여러 컬럼이 걸리면 모호하므로 채택하지 않는다.)
    private static int FindColumn(string[] headerCells, params string[] names)
        => HeaderMatch.FindIndex(headerCells, names);

    private static bool TryParseDouble(string text, out double value)
        => HeaderMatch.TryParseNumber(text, out value);
}
