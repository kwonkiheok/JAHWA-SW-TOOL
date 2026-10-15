using System.IO;

namespace MeasureHub.Services;

// DW(동운, DW9828) Circle Test 로그.
// 1행이 컬럼 헤더이고 전문(preamble)·시간 컬럼이 없다:
//   X Target(@12bit), Y Target(@12bit), X Position(@12bit), Y Position(@12bit), X Diff(@12bit), Y Diff(@12bit)
// 한 파일이 1 cycle(100 point, 360°/3.6°)이다. 시간은 행 인덱스(0, 1, 2 …)로 둔다(DW Sine 과 같은 방식).
// Target → ReadVt, Position → Hall 로 담는다. Diff 는 파일 값(= Target - Position)을 쓰고,
// Diff 컬럼이 없는 파일이면 Target - Position 으로 직접 계산한다.
public static class DwCircleTestReader
{
    private static readonly string[] TargetX = ["X Target(@12bit)", "X Target 12bit", "X Target"];
    private static readonly string[] TargetY = ["Y Target(@12bit)", "Y Target 12bit", "Y Target"];
    private static readonly string[] PositionX = ["X Position(@12bit)", "X Position 12bit", "X Position"];
    private static readonly string[] PositionY = ["Y Position(@12bit)", "Y Position 12bit", "Y Position"];
    private static readonly string[] DiffXNames = ["X Diff(@12bit)", "X Diff 12bit", "X Diff"];
    private static readonly string[] DiffYNames = ["Y Diff(@12bit)", "Y Diff 12bit", "Y Diff"];

    public static CircleReadResult Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Empty DW CircleTest file.");

        int headerRow = -1;
        string[] headerCells = Array.Empty<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            var cells = lines[i].Split(',');
            // Target/Position X·Y 4개가 모두 있는 행을 헤더 행으로 본다(DwSinewaveReader 와 같은 규칙).
            if (HeaderMatch.FindIndex(cells, TargetX) >= 0
                && HeaderMatch.FindIndex(cells, PositionX) >= 0
                && HeaderMatch.FindIndex(cells, TargetY) >= 0
                && HeaderMatch.FindIndex(cells, PositionY) >= 0)
            {
                headerRow = i;
                headerCells = cells;
                break;
            }
        }

        if (headerRow < 0)
            throw new InvalidOperationException("'X Target(@12bit)' header row was not found.");

        int targetXIndex = HeaderMatch.FindIndex(headerCells, TargetX);
        int targetYIndex = HeaderMatch.FindIndex(headerCells, TargetY);
        int positionXIndex = HeaderMatch.FindIndex(headerCells, PositionX);
        int positionYIndex = HeaderMatch.FindIndex(headerCells, PositionY);
        int diffXIndex = HeaderMatch.FindIndex(headerCells, DiffXNames);
        int diffYIndex = HeaderMatch.FindIndex(headerCells, DiffYNames);

        var time = new List<double>();
        var readVtX = new List<double>();
        var hallX = new List<double>();
        var diffX = new List<double>();
        var readVtY = new List<double>();
        var hallY = new List<double>();
        var diffY = new List<double>();

        int maxIndex = new[] { targetXIndex, targetYIndex, positionXIndex, positionYIndex }.Max();
        double t = 0;
        for (int i = headerRow + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = line.Split(',');
            if (cells.Length <= maxIndex)
                continue;

            if (!HeaderMatch.TryParseNumber(cells[targetXIndex], out var tx)
                || !HeaderMatch.TryParseNumber(cells[positionXIndex], out var px)
                || !HeaderMatch.TryParseNumber(cells[targetYIndex], out var ty)
                || !HeaderMatch.TryParseNumber(cells[positionYIndex], out var py))
                continue;

            time.Add(t);
            readVtX.Add(tx);
            hallX.Add(px);
            diffX.Add(ReadDiff(cells, diffXIndex, tx - px));
            readVtY.Add(ty);
            hallY.Add(py);
            diffY.Add(ReadDiff(cells, diffYIndex, ty - py));
            t += 1;
        }

        if (time.Count < 2)
            throw new InvalidOperationException("Not enough valid DW CircleTest data rows.");

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

    private static double ReadDiff(string[] cells, int index, double fallback)
        => index >= 0 && index < cells.Length && HeaderMatch.TryParseNumber(cells[index], out var d) ? d : fallback;
}
