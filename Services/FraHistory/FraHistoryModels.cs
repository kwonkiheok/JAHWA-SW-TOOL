namespace MeasureHub.Services;

/// <summary>FRA History 문서의 시료 1개 (MeasureHub FRA 결과 + Bode 데이터).</summary>
public sealed record FraHistorySample(
    string FileName,
    string Judge,
    double? Gain10Hz,
    double? GM,
    double? PM,
    double? PM3,
    IReadOnlyList<BodePoint> Points);

/// <summary>한 축(OIS X / OIS Y / AF)의 그룹별 시료. Groups 순서가 문서의 Group 1~4 순서다.</summary>
public sealed record FraHistoryAxis(
    string Axis,
    IReadOnlyList<(string GroupName, IReadOnlyList<FraHistorySample> Samples)> Groups);

/// <summary>빌드 1회 = 문서의 블록 1개.</summary>
public sealed record FraHistoryBuild(
    string Stage,
    string Maker,
    string Date,
    string Owner,
    string Reason,
    string Opinion,
    IReadOnlyList<FraHistoryAxis> Axes);

/// <summary>블록 헤더 6칸.</summary>
public sealed record FraHistoryHeader(string Stage, string Maker, string Date, string Owner, string Reason, string Opinion);

/// <summary>문서에 이미 들어간 빌드 블록 (StartRow ~ EndRow).</summary>
public sealed record FraHistoryBlockInfo(int StartRow, int EndRow, FraHistoryHeader Header);

/// <summary>Spec 미달 표시에 쓰는 하한값. null 이면 비교하지 않는다.</summary>
public sealed record FraHistorySpec(double? Gm, double? Pm, double? Pm3);
