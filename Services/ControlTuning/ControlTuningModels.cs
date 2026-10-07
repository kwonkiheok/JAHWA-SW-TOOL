namespace MeasureHub.Services;

// ---------------- 공통 ----------------

/// <summary>블록 헤더 6칸 (개발 단계 ~ 검토 의견).</summary>
public sealed record HistoryHeader(string Stage, string Maker, string Date, string Owner, string Reason, string Opinion);

/// <summary>문서에 이미 들어간 빌드 블록 (StartRow ~ EndRow).</summary>
public sealed record HistoryBlockInfo(int StartRow, int EndRow, HistoryHeader Header);

// ---------------- FRA ----------------

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
public sealed record FraHistoryBuild(HistoryHeader Header, IReadOnlyList<FraHistoryAxis> Axes);

/// <summary>Spec 미달 표시에 쓰는 하한값. null 이면 비교하지 않는다.</summary>
public sealed record FraHistorySpec(double? Gm, double? Pm, double? Pm3);

// ---------------- DC Sensitivity ----------------

/// <summary>DC History 문서의 시료 1개 (MeasureHub DC 결과의 Up/Down 곡선).</summary>
public sealed record DcHistorySample(
    string FileName,
    string InputLabel,
    string OutputLabel,
    IReadOnlyList<double> UpInput,
    IReadOnlyList<double> UpOutput,
    IReadOnlyList<double> DownInput,
    IReadOnlyList<double> DownOutput);

/// <summary>한 축의 DC 시료. Representative 는 대표 파형으로 그릴 시료(Samples 안의 하나).</summary>
public sealed record DcHistoryAxis(string Axis, IReadOnlyList<DcHistorySample> Samples, DcHistorySample Representative);

public sealed record DcHistoryBuild(HistoryHeader Header, IReadOnlyList<DcHistoryAxis> Axes);
