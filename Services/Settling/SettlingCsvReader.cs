using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;

namespace MeasureHub.Services;

public sealed record SettlingSeries(
    string Label,
    IReadOnlyList<double> TimeMs,
    IReadOnlyList<double> Value,
    double? ExplicitTarget);

public static class SettlingCsvReader
{
    // DW: N줄의 key:value preamble 후 ",Target,X Position,Y Position" 헤더.
    // 첫 컬럼(이름 없음)은 항상 Time 인덱스이므로 후보명 매칭 없이 index 0을 그대로 사용한다.
    internal static IReadOnlyList<SettlingSeries> ReadDw(string csvPath)
    {
        var rows = ReadAllRows(csvPath);
        if (rows.Count == 0)
            throw new InvalidOperationException("Empty CSV file.");

        var targetCandidates = new[] { "target" };
        var xCandidates = new[] { "xposition", "x position", "x" };
        var yCandidates = new[] { "yposition", "y position", "y" };

        int headerRow = DetectHeaderRow(rows, targetCandidates, xCandidates, 40);
        if (headerRow < 0)
            throw new InvalidOperationException("DW Settling header (Target/X Position) was not found.");

        var header = rows[headerRow].Select(x => (x ?? string.Empty).Trim()).ToList();
        var targetCol = FindBestMatchingColumn(header, targetCandidates);
        var xCol = FindBestMatchingColumn(header, xCandidates);
        var yCol = FindBestMatchingColumn(header, yCandidates);
        if (targetCol is null || xCol is null || yCol is null)
            throw new InvalidOperationException("Failed to match DW Settling Target/X/Y columns.");

        int it = header.FindIndex(x => string.Equals(x, targetCol, StringComparison.Ordinal));
        int ix = header.FindIndex(x => string.Equals(x, xCol, StringComparison.Ordinal));
        int iy = header.FindIndex(x => string.Equals(x, yCol, StringComparison.Ordinal));

        var time = new List<double>();
        var targets = new List<double>();
        var xs = new List<double>();
        var ys = new List<double>();

        for (int r = headerRow + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 0 || it >= row.Count || ix >= row.Count || iy >= row.Count)
                continue;

            if (!TryParseFlexibleDouble(row[0], out var tv)) continue;
            if (!TryParseFlexibleDouble(row[it], out var targetV)) continue;
            if (!TryParseFlexibleDouble(row[ix], out var xv)) continue;
            if (!TryParseFlexibleDouble(row[iy], out var yv)) continue;

            time.Add(tv);
            targets.Add(targetV);
            xs.Add(xv);
            ys.Add(yv);
        }

        if (time.Count < 2)
            throw new InvalidOperationException("Not enough valid DW Settling data rows.");

        // DW 파일은 Target 이 중심 -> 위 -> 중심 -> 아래 -> 중심 으로 바뀌는 사각파다.
        // 예전에는 targets[0] 하나만 전 구간에 적용해서, 허용범위 밴드가 그래프 한가운데
        // 그어지고 위/아래 이동 구간이 전부 밴드 밖으로 잡혔다(정착시간 254ms, 오버슈트 401 등).
        // 설비는 "중심으로 돌아오는 스텝"만 판정하며(헤더 X Settling Index 154/255),
        // 그 두 스텝이 각각 아래로 복귀(Down)와 위로 복귀(Up)에 해당한다.
        return SplitReturnSteps(time, targets, xs, ys);
    }

    // Target 이 바뀌는 지점마다 구간을 자르고, 중심(첫 Target)으로 돌아오는 스텝만 남긴다.
    // 이동 방향(스텝 시작값 대비 목표가 큰지)으로 Up/Down 을 구분해 라벨에 넣는다.
    // 전환 이전 baseline 구간은 트리거가 없어 판정 대상이 아니므로 제외한다
    // (SquareMetrics.DetectSteps 와 같은 규칙).
    private static IReadOnlyList<SettlingSeries> SplitReturnSteps(
        IReadOnlyList<double> time,
        IReadOnlyList<double> targets,
        IReadOnlyList<double> xs,
        IReadOnlyList<double> ys)
    {
        int n = time.Count;
        double home = targets[0];

        var steps = new List<(int Start, int End, double Target)>();
        int stepStart = -1;
        double current = targets[0];
        for (int i = 1; i < n; i++)
        {
            if (targets[i] == current) continue;
            if (stepStart >= 0) steps.Add((stepStart, i, current));
            stepStart = i;
            current = targets[i];
        }
        if (stepStart >= 0) steps.Add((stepStart, n, current));

        var result = new List<SettlingSeries>();
        int upCount = 0, downCount = 0;

        foreach (var (start, end, target) in steps)
        {
            if (target != home) continue;              // 중심으로 돌아오는 스텝만
            if (end - start < 2) continue;

            var segTime = time.Skip(start).Take(end - start).ToList();
            var segX = xs.Skip(start).Take(end - start).ToList();
            var segY = ys.Skip(start).Take(end - start).ToList();

            bool isUp = target > xs[start];
            string dir = isUp ? "Up" : "Down";
            int idx = isUp ? ++upCount : ++downCount;
            string suffix = (isUp ? upCount : downCount) > 1 || idx > 1 ? $" #{idx}" : "";

            result.Add(new SettlingSeries($"X {dir}{suffix}", segTime, segX, target));
            result.Add(new SettlingSeries($"Y {dir}{suffix}", segTime, segY, target));
        }

        if (result.Count == 0)
        {
            // 스텝을 못 찾으면(Target 이 끝까지 일정한 파일) 기존처럼 전 구간을 하나로 본다.
            double target = targets[0];
            result.Add(new SettlingSeries("X", time, xs, target));
            result.Add(new SettlingSeries("Y", time, ys, target));
        }

        return result;
    }

    // Robocube: preamble 없이 2행 헤더. 1행 = 그룹 라벨("Start NCode  Step Mum")이 그룹 시작 컬럼에만 있고
    // 나머지는 "Count:k"가 2개씩 짝지어 반복. 2행 = 그룹마다 "Time[ms],Posi[um]×N".
    // 그룹 개수/폭이 파일마다 다르므로 2행에서 "Time[ms]" 등장 위치로 그룹 시작 컬럼을 동적으로 찾는다.
    internal static IReadOnlyList<SettlingSeries> ReadRobocube(string csvPath)
    {
        var rows = ReadAllRows(csvPath);
        if (rows.Count < 3)
            throw new InvalidOperationException("Robocube Settling file has too few rows.");

        var groupLabelRow = rows[0];
        var subHeaderRow = rows[1];
        string timeMarker = NormalizeName("Time[ms]");

        var groupStarts = new List<int>();
        for (int c = 0; c < subHeaderRow.Count; c++)
        {
            if (string.Equals(NormalizeName(subHeaderRow[c]), timeMarker, StringComparison.Ordinal))
                groupStarts.Add(c);
        }

        if (groupStarts.Count == 0)
            throw new InvalidOperationException("Robocube Settling: no Time[ms] group markers found.");

        var groupLabelRegex = new Regex(@"Start\s+(-?\d+)\s*Code\s+Step\s+(-?\d+)\s*um", RegexOptions.IgnoreCase);
        var result = new List<SettlingSeries>();

        for (int g = 0; g < groupStarts.Count; g++)
        {
            int start = groupStarts[g];
            int end;
            if (g + 1 < groupStarts.Count)
            {
                end = groupStarts[g + 1];
            }
            else
            {
                // 마지막 그룹은 다음 그룹 시작 컬럼이 없어 subHeaderRow.Count를 그대로 쓰면
                // 소스 csv 끝의 트레일링 빈 컬럼(콤마 하나 더 붙는 export 특성)까지 그룹에
                // 포함돼 매 행 파싱이 실패하고 그룹 전체가 버려진다. 끝쪽 빈/공백 컬럼을 잘라낸다.
                end = subHeaderRow.Count;
                while (end > start && string.IsNullOrWhiteSpace(subHeaderRow[end - 1]))
                    end--;
            }
            int dataColCount = (end - start) - 1; // Time 컬럼 제외
            if (dataColCount < 2)
                continue;

            int countN = dataColCount / 2; // 2컬럼당 1회 반복(Count) 측정으로 간주
            if (countN < 1)
                continue;

            string rawLabel = start < groupLabelRow.Count ? (groupLabelRow[start] ?? "").Trim() : $"Group{g + 1}";
            var m = groupLabelRegex.Match(rawLabel);
            string groupLabel = m.Success
                ? $"Start {m.Groups[1].Value}Code Step {m.Groups[2].Value}um"
                : (string.IsNullOrWhiteSpace(rawLabel) ? $"Group{g + 1}" : rawLabel);

            var timeList = new List<double>();
            var valueLists = new List<double>[dataColCount];
            for (int k = 0; k < dataColCount; k++)
                valueLists[k] = new List<double>();

            for (int r = 2; r < rows.Count; r++)
            {
                var row = rows[r];
                if (start >= row.Count || !TryParseFlexibleDouble(row[start], out var tv))
                    continue;

                bool rowOk = true;
                var vals = new double[dataColCount];
                for (int k = 0; k < dataColCount; k++)
                {
                    int col = start + 1 + k;
                    if (col >= row.Count || !TryParseFlexibleDouble(row[col], out vals[k]))
                    {
                        rowOk = false;
                        break;
                    }
                }

                if (!rowOk)
                    continue;

                timeList.Add(tv);
                for (int k = 0; k < dataColCount; k++)
                    valueLists[k].Add(vals[k]);
            }

            if (timeList.Count < 2)
                continue;

            // 2개 컬럼은 서로 반대 방향으로 움직이는 독립된 채널(A/B)이라 평균 내면 상쇄된다.
            // 각 채널을 별도 시리즈로 남겨서 채널별로 따로 시각화·계산한다.
            for (int cnt = 0; cnt < countN; cnt++)
            {
                var col1 = valueLists[cnt * 2];
                var col2 = valueLists[cnt * 2 + 1];
                int n = new[] { col1.Count, col2.Count, timeList.Count }.Min();
                if (n < 2)
                    continue;

                var time = timeList.Take(n).ToList();
                result.Add(new SettlingSeries($"{groupLabel} A #{cnt + 1}", time, col1.Take(n).ToList(), null));
                result.Add(new SettlingSeries($"{groupLabel} B #{cnt + 1}", time, col2.Take(n).ToList(), null));
            }
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Robocube Settling: no valid groups parsed.");

        return result;
    }

    private const double KStarMsPerRow = 0.1;
    private const int KStarMaxRows = 1000;

    // K-star: xlsx 파일. "Group_Data" 시트에 조건마다 "Laser NN"/"Current NN" 컬럼 쌍이 있고(Laser만 사용,
    // Current는 무시), "Settling_Group" 시트에 조건(Start/End Code)과 조건당 반복 횟수("Settling Time NN"
    // 컬럼 개수)가 정의돼 있다. Laser 컬럼은 순서대로 조건마다 반복 횟수만큼 소비한다(조건1 → 앞쪽 N개,
    // 조건2 → 다음 N개, ...). 1~1000행만 유효하고 그 뒤는 장비가 버퍼를 재사용해 남은 이전 테스트 잔재라
    // 읽지 않는다. Time[ms] 컬럼이 없어 행 번호 × 0.1ms를 시간축으로 쓴다.
    internal static IReadOnlyList<SettlingSeries> ReadKStar(string xlsxPath)
    {
        using var wb = new XLWorkbook(xlsxPath);
        if (!wb.Worksheets.TryGetWorksheet("Group_Data", out var dataWs) ||
            !wb.Worksheets.TryGetWorksheet("Settling_Group", out var groupWs))
            throw new InvalidOperationException("K-star Settling: Group_Data/Settling_Group sheet not found.");

        var laserCols = new List<int>();
        int lastCol = dataWs.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            if (dataWs.Cell(1, c).GetString().Trim().StartsWith("Laser", StringComparison.OrdinalIgnoreCase))
                laserCols.Add(c);
        }

        if (laserCols.Count == 0)
            throw new InvalidOperationException("K-star Settling: no Laser columns found in Group_Data.");

        int lastGroupCol = groupWs.LastColumnUsed()?.ColumnNumber() ?? 0;
        int repsPerCondition = 0;
        for (int c = 1; c <= lastGroupCol; c++)
        {
            if (groupWs.Cell(1, c).GetString().Trim().StartsWith("Settling Time", StringComparison.OrdinalIgnoreCase))
                repsPerCondition++;
        }

        if (repsPerCondition == 0)
            throw new InvalidOperationException("K-star Settling: no 'Settling Time' columns found in Settling_Group.");

        var timeMs = Enumerable.Range(1, KStarMaxRows).Select(i => i * KStarMsPerRow).ToList();
        var result = new List<SettlingSeries>();

        int laserIndex = 0;
        int conditionRow = 2;
        while (!groupWs.Cell(conditionRow, 1).IsEmpty())
        {
            string startCode = groupWs.Cell(conditionRow, 1).GetString().Trim();
            string endCode = groupWs.Cell(conditionRow, 2).GetString().Trim();
            string groupLabel = $"Start {startCode} End {endCode}";

            for (int rep = 0; rep < repsPerCondition && laserIndex < laserCols.Count; rep++, laserIndex++)
            {
                int col = laserCols[laserIndex];
                var values = new List<double>(KStarMaxRows);
                bool ok = true;

                for (int r = 0; r < KStarMaxRows; r++)
                {
                    var cell = dataWs.Cell(2 + r, col);
                    if (cell.IsEmpty())
                    {
                        ok = false;
                        break;
                    }
                    values.Add(cell.GetValue<double>());
                }

                if (!ok || values.Count < 2)
                    continue;

                result.Add(new SettlingSeries($"{groupLabel} #{rep + 1}", timeMs, values, null));
            }

            conditionRow++;
        }

        if (result.Count == 0)
            throw new InvalidOperationException("K-star Settling: no valid groups parsed.");

        return result;
    }

    // Anosys: 헤더 없는 순수 숫자 CSV. (Time,Value) 쌍이 채널 수만큼 반복(현재 확인된 샘플은 4채널/8열 —
    // 앞쪽 절반은 Position(위치), 뒤쪽 절반은 Current(전류)). 스텝 트리거 전 baseline 구간 없이 t=0부터
    // 기록되므로 DW와 같이 JudgeStartTimeMs=0을 그대로 쓴다.
    // Current는 K-star가 "Laser NN"/"Current NN" 쌍에서 Current 컬럼을 무시하는 것과 동일하게 Settling
    // 판정 대상에서 제외한다 — Position 채널만 파싱해서 반환한다.
    internal static IReadOnlyList<SettlingSeries> ReadAnosys(string csvPath)
    {
        var rows = ReadAllRows(csvPath).Where(r => r.Count > 0).ToList();
        if (rows.Count < 2)
            throw new InvalidOperationException("Anosys Settling file has too few rows.");

        int colCount = rows.Max(r => r.Count);
        if (colCount < 2 || colCount % 2 != 0)
            throw new InvalidOperationException("Anosys Settling: unexpected column count (Time/Value pairs expected).");

        int channelCount = colCount / 2;
        if (channelCount % 2 != 0)
            throw new InvalidOperationException("Anosys Settling: expected an even channel count (Position + Current pairs).");

        int positionChannelCount = channelCount / 2;
        var timeLists = Enumerable.Range(0, positionChannelCount).Select(_ => new List<double>()).ToArray();
        var valueLists = Enumerable.Range(0, positionChannelCount).Select(_ => new List<double>()).ToArray();

        foreach (var row in rows)
        {
            if (row.Count < colCount) continue;

            for (int c = 0; c < positionChannelCount; c++)
            {
                if (!TryParseFlexibleDouble(row[c * 2], out var t)) continue;
                if (!TryParseFlexibleDouble(row[c * 2 + 1], out var v)) continue;
                timeLists[c].Add(t);
                valueLists[c].Add(v);
            }
        }

        var result = new List<SettlingSeries>();
        for (int c = 0; c < positionChannelCount; c++)
        {
            if (timeLists[c].Count < 2) continue;
            result.Add(new SettlingSeries($"Position{c + 1}", timeLists[c], valueLists[c], null));
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Anosys Settling: no valid channel data parsed.");

        return result;
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

    private static int DetectHeaderRow(
        IReadOnlyList<List<string>> rows,
        IEnumerable<string> candidatesA,
        IEnumerable<string> candidatesB,
        int maxScanRows)
    {
        var normA = candidatesA.Select(NormalizeName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var normB = candidatesB.Select(NormalizeName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        for (int rowIdx = 0; rowIdx < Math.Min(maxScanRows, rows.Count); rowIdx++)
        {
            var values = rows[rowIdx].Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeName).ToList();
            bool hasA = normA.Any(candidate => values.Any(cell => cell == candidate));
            bool hasB = normB.Any(candidate => values.Any(cell => cell == candidate));
            if (hasA && hasB)
                return rowIdx;
        }

        return -1;
    }

    // 헤더 이름 변형 대응(정확 -> 접두 -> 부분 일치)은 HeaderMatch가 담당한다.
    private static string? FindBestMatchingColumn(IEnumerable<string> columns, IEnumerable<string> candidates)
        => HeaderMatch.FindName(columns, candidates);

    // 정규화 규칙은 HeaderMatch 한 곳에서만 관리한다.
    private static string NormalizeName(string? name) => HeaderMatch.Normalize(name);

    private static bool TryParseFlexibleDouble(string? s, out double value)
        => HeaderMatch.TryParseNumber(s, out value);
}
