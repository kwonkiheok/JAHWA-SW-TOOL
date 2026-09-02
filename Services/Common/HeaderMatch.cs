using System.Globalization;

namespace MeasureHub.Services;

/// <summary>
/// 설비사·장비 버전에 따라 컬럼 헤더 이름이 조금씩 달라지는 문제에 대응하는 공용 헤더 매칭 유틸리티.
///
/// 실제 샘플에서 확인된 변형 예:
///   - AKM SINE WAVE : "Hall_X"          ↔ "Hall_X[filter set]"   (뒤에 표기가 덧붙음)
///   - AKM SQUARE    : "time[ms],pos,read_vt,hall,read_mv" ↔ "time[ms],pos,hall"  (컬럼 수 자체가 다름)
///   - AKM 계열 전문 : "TestName," ↔ "TestName ,"                 (뒤 공백)
///
/// 매칭은 아래 순서로 단계를 낮춰가며 시도한다. 위 단계에서 찾으면 아래는 보지 않는다.
///   1) 정확 일치(정규화 후)
///   2) 접두 일치 — 단, 해당 후보에 걸리는 컬럼이 "정확히 하나"일 때만 채택
///   3) 부분 일치 — 역시 하나일 때만 채택
///
/// 2)·3)에서 여러 컬럼이 걸리면 채택하지 않고 다음 후보로 넘어간다. 추측으로 엉뚱한 컬럼을 잡는 것보다
/// 못 찾는 편이 안전하기 때문이다. 예를 들어 DW SINE WAVE 파일에는
/// "X Target %" 와 "X Target 12bit" 가 함께 들어 있어, 접두 일치를 무조건 허용하면
/// "X Target" 후보가 두 컬럼에 모두 걸린다. 이 경우 단계 2를 건너뛰고 정확 일치로만 구분된다.
/// </summary>
public static class HeaderMatch
{
    private static readonly string[] StripTokens = [" ", "_", "[", "]", "(", ")", "-", "/"];

    /// <summary>
    /// 헤더 이름 비교용 정규화. 소문자로 바꾸고 공백·밑줄·괄호·대괄호·하이픈·슬래시를 제거한다.
    /// 단위나 퍼센트 표기(%, 12bit 등)는 <b>제거하지 않는다</b> — DW SINE WAVE 처럼
    /// 단위 표기가 컬럼을 구분하는 유일한 정보인 파일이 있기 때문이다.
    /// </summary>
    public static string Normalize(string? name)
    {
        var s = (name ?? string.Empty).Trim().ToLowerInvariant().Replace("°", "deg");
        foreach (var token in StripTokens)
            s = s.Replace(token, string.Empty);
        return s;
    }

    /// <summary>
    /// 헤더 목록에서 후보 이름에 해당하는 컬럼 인덱스를 찾는다. 못 찾으면 -1.
    /// </summary>
    /// <param name="headers">파일에서 읽은 헤더 셀 목록(원본 문자열 그대로 전달해도 된다).</param>
    /// <param name="candidates">우선순위 순서대로 나열한 후보 이름.</param>
    /// <param name="allowFuzzy">false면 정확 일치만 사용한다.</param>
    public static int FindIndex(IReadOnlyList<string?> headers, IEnumerable<string> candidates, bool allowFuzzy = true)
    {
        if (headers is null || headers.Count == 0)
            return -1;

        var normalizedHeaders = new string[headers.Count];
        for (int i = 0; i < headers.Count; i++)
            normalizedHeaders[i] = Normalize(headers[i]);

        var normalizedCandidates = candidates?
            .Select(Normalize)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList() ?? [];

        if (normalizedCandidates.Count == 0)
            return -1;

        // 1) 정확 일치 — 후보 우선순위 순서로 본다.
        foreach (var candidate in normalizedCandidates)
        {
            for (int i = 0; i < normalizedHeaders.Length; i++)
            {
                if (normalizedHeaders[i].Length > 0 && normalizedHeaders[i] == candidate)
                    return i;
            }
        }

        if (!allowFuzzy)
            return -1;

        // 2) 접두 일치 — "Hall_X" 후보가 "Hall_X[filter set]" 헤더에 걸리는 경우.
        int found = FindUnique(normalizedHeaders, normalizedCandidates,
            (header, candidate) => header.StartsWith(candidate, StringComparison.Ordinal));
        if (found >= 0)
            return found;

        // 3) 부분 일치 — 앞에 접두어가 붙는 변형까지 커버한다.
        return FindUnique(normalizedHeaders, normalizedCandidates,
            (header, candidate) => header.Contains(candidate, StringComparison.Ordinal));
    }

    /// <summary>
    /// 후보마다 조건에 걸리는 컬럼이 정확히 하나일 때만 그 인덱스를 돌려준다.
    /// 둘 이상이면 모호하므로 채택하지 않고 다음 후보로 넘어간다.
    /// </summary>
    private static int FindUnique(
        IReadOnlyList<string> normalizedHeaders,
        IReadOnlyList<string> normalizedCandidates,
        Func<string, string, bool> predicate)
    {
        foreach (var candidate in normalizedCandidates)
        {
            int hit = -1;
            int count = 0;
            for (int i = 0; i < normalizedHeaders.Count; i++)
            {
                if (normalizedHeaders[i].Length == 0)
                    continue;
                if (!predicate(normalizedHeaders[i], candidate))
                    continue;

                count++;
                if (count > 1)
                    break;
                hit = i;
            }

            if (count == 1)
                return hit;
        }

        return -1;
    }

    /// <summary>
    /// <see cref="FindIndex"/>와 같은 규칙으로 찾되, 인덱스가 아니라 원본 헤더 문자열을 돌려준다.
    /// 이름으로 컬럼을 참조하는 기존 리더(SCAN·DC SENSITIVITY)를 위한 형태다.
    /// </summary>
    public static string? FindName(IEnumerable<string?> headers, IEnumerable<string> candidates, bool allowFuzzy = true)
    {
        var list = headers?.ToList() ?? [];
        int index = FindIndex(list, candidates, allowFuzzy);
        return index >= 0 ? list[index] : null;
    }

    /// <summary>
    /// 헤더 행 후보를 찾는다. 지정한 역할들의 후보가 <b>모두</b> 발견되는 첫 행을 헤더 행으로 본다.
    /// 못 찾으면 -1.
    /// </summary>
    /// <param name="rows">파일을 행 단위로 읽은 결과.</param>
    /// <param name="maxScanRows">위에서부터 몇 행까지 훑을지.</param>
    /// <param name="candidateGroups">역할별 후보 묶음. 예: [Input 후보들, Output 후보들]</param>
    public static int FindHeaderRow(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int maxScanRows,
        params IEnumerable<string>[] candidateGroups)
    {
        if (rows is null || rows.Count == 0 || candidateGroups is null || candidateGroups.Length == 0)
            return -1;

        var groups = candidateGroups.Select(g => g?.ToList() ?? []).ToList();

        int limit = Math.Min(maxScanRows, rows.Count);
        for (int rowIdx = 0; rowIdx < limit; rowIdx++)
        {
            var cells = rows[rowIdx];
            if (cells is null || cells.Count == 0)
                continue;

            bool allFound = true;
            foreach (var group in groups)
            {
                if (group.Count == 0)
                    continue;
                if (FindIndex(cells!, group) < 0)
                {
                    allFound = false;
                    break;
                }
            }

            if (allFound)
                return rowIdx;
        }

        return -1;
    }

    /// <summary>
    /// 숫자 파싱 단일 정책. InvariantCulture로 먼저 시도하고, 실패하면 현재 문화권으로 한 번 더 시도한다.
    /// (기존에 리더마다 Invariant만 쓰는 곳과 두 가지를 다 쓰는 곳이 섞여 있었다.)
    /// </summary>
    public static bool TryParseNumber(string? s, out double value)
    {
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return true;
        value = 0;
        return false;
    }
}
