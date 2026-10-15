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

// ---------------- Settling Time ----------------

/// <summary>
/// 시료 1개의 조건(스텝) 하나에 대한 MeasureHub Settling 결과 + 그래프 재료.
/// Result / ToleranceBand / Displacement 는 MeasureHub 화면·PNG 와 같은 그래프를 그리는 데 쓴다.
/// </summary>
public sealed record SettlingConditionData(
    string Label,
    double? SettlingTimeMs,
    string Judge,
    IReadOnlyList<SettlingSeries> Series,
    SettlingResult Result,
    double ToleranceBand,
    double Displacement);

public sealed record SettlingHistorySample(string FileName, IReadOnlyList<SettlingConditionData> Conditions)
{
    public SettlingConditionData? Find(string label) => Conditions.FirstOrDefault(c => c.Label == label);
}

/// <summary>
/// 한 축의 Settling 시료. Conditions = 표·그래프에 넣을 조건(순서대로, 최대 8 / 그래프는 앞 4개),
/// Groups 순서가 표의 그룹 순서, Representative 는 대표 파형을 그릴 시료.
/// </summary>
public sealed record SettlingHistoryAxis(
    string Axis,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<(string GroupName, IReadOnlyList<SettlingHistorySample> Samples)> Groups,
    SettlingHistorySample Representative);

public sealed record SettlingHistoryBuild(HistoryHeader Header, IReadOnlyList<SettlingHistoryAxis> Axes);

/// <summary>Max Settling Time(ms). 넘으면 빨간색. null 이면 비교하지 않는다.</summary>
public sealed record SettlingHistorySpec(double? MaxSettlingTimeMs);

// ---------------- Sine Wave ----------------

/// <summary>
/// Sine Wave 시료 1개 (MeasureHub SINE WAVE 결과 + X/Y 파형).
/// ConditionHint 는 파일 머리의 Sine Freq / Amplitude 로 만든 조건 이름 제안 (없으면 빈 문자열).
/// </summary>
public sealed record SinewaveHistorySample(
    string FileName,
    string Judge,
    double? MaxErrX,
    double? MaxErrY,
    IReadOnlyList<double> TimeMs,
    IReadOnlyList<double> PosiX,
    IReadOnlyList<double> HallX,
    IReadOnlyList<double> PosiY,
    IReadOnlyList<double> HallY,
    string ConditionHint);

/// <summary>측정 조건 하나 (예: Test1 / 6Hz 80%). Groups 순서가 표의 그룹 순서, Representative 는 대표 파형 시료.</summary>
public sealed record SinewaveHistoryCondition(
    string Name,
    IReadOnlyList<(string GroupName, IReadOnlyList<SinewaveHistorySample> Samples)> Groups,
    SinewaveHistorySample Representative);

public sealed record SinewaveHistoryBuild(HistoryHeader Header, IReadOnlyList<SinewaveHistoryCondition> Conditions);

/// <summary>|MaxErr| 상한(code). 넘으면 빨간색. null 이면 비교하지 않는다.</summary>
public sealed record SinewaveHistorySpec(double? MaxErr);

// ---------------- Circle ----------------

/// <summary>
/// Circle Test 시료 1개 (MeasureHub CIRCLE TEST 결과 + XY 궤적). MaxErr 는 code.
/// ConditionHint 는 파일 머리의 Sine Freq / Amplitude(AKM) 또는 파일명의 "6Hz"(DW) 로 만든 조건 이름 제안.
/// </summary>
public sealed record CircleHistorySample(
    string FileName,
    string Judge,
    double? MaxErrX,
    double? MaxErrY,
    IReadOnlyList<double> ReadVtX,
    IReadOnlyList<double> ReadVtY,
    IReadOnlyList<double> HallX,
    IReadOnlyList<double> HallY,
    string ConditionHint);

/// <summary>측정 조건 하나 (예: Test1 / 6Hz 85%). Groups 순서가 표의 그룹 순서, Representative 는 대표 궤적 시료.</summary>
public sealed record CircleHistoryCondition(
    string Name,
    IReadOnlyList<(string GroupName, IReadOnlyList<CircleHistorySample> Samples)> Groups,
    CircleHistorySample Representative);

public sealed record CircleHistoryBuild(HistoryHeader Header, IReadOnlyList<CircleHistoryCondition> Conditions);

/// <summary>|MaxErr| 상한(code, MeasureHub CIRCLE Spec). 넘으면 빨간색. null 이면 비교하지 않는다.</summary>
public sealed record CircleHistorySpec(double? MaxErr);
