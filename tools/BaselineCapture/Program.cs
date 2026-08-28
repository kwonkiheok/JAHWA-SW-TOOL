using System.Globalization;
using System.IO;
using System.Text;
using JahwaInspector.Light.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  기준선 캡처 (Baseline Capture)
//
//  목적: 리팩터링 전후로 "계산 결과가 한 글자도 안 바뀌었는지" 증명하기 위한 도구.
//        실제 샘플 데이터를 전부 읽어 각 검사의 지표를 계산하고, 결정적(deterministic)
//        텍스트로 출력한다. 리팩터링 후 다시 돌려서 diff 했을 때 차이가 0이어야 한다.
//
//  사용법:  dotnet run -- <출력파일경로>
//
//  주의: 이 도구는 앱 코드를 읽기만 하고 수정하지 않는다.
//        부동소수 표기 차이로 인한 거짓 변경을 막기 위해 소수 6자리로 고정 출력한다.
// ─────────────────────────────────────────────────────────────────────────────

string outPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "baseline.txt");

string dataRoot = @"D:\중점추진과제 SW\workspace\SW DATA\SW DATA";
string fraRoot = @"D:\ProgramData\DATA";

var sb = new StringBuilder();
int okCount = 0, errCount = 0;

// 숫자를 항상 같은 형식으로 — null 은 "N/A"
static string N(double? v) =>
    v is null ? "N/A"
    : double.IsNaN(v.Value) ? "NaN"
    : v.Value.ToString("0.000000", CultureInfo.InvariantCulture);

static string N2(double v) => N(v);

void Line(string inspection, string dataType, string file, string metrics)
    => sb.AppendLine($"{inspection}\t{dataType}\t{file}\t{metrics}");

void Fail(string inspection, string dataType, string file, Exception ex)
{
    sb.AppendLine($"{inspection}\t{dataType}\t{file}\tERROR={ex.GetType().Name}: {ex.Message}");
    errCount++;
}

// 폴더 안 파일을 항상 같은 순서로
static string[] Files(string dir, string pattern = "*.csv")
    => Directory.Exists(dir)
        ? Directory.GetFiles(dir, pattern).OrderBy(x => x, StringComparer.Ordinal).ToArray()
        : Array.Empty<string>();

static string Name(string p) => Path.GetFileName(p);

// ── FRA ──────────────────────────────────────────────────────────────────────
// 원본 데이터가 1800개 넘어 시간이 걸리므로, 헤더가 Frequency 로 시작하는 것만
// 파일명 순으로 앞에서 200개만 쓴다(결정적).
{
    var fraFiles = Directory.Exists(fraRoot)
        ? Directory.EnumerateFiles(fraRoot, "*.csv", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\OUTPUT\", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.Ordinal)
            .Where(f =>
            {
                try { using var sr = new StreamReader(f); return (sr.ReadLine() ?? "").StartsWith("Frequency", StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            })
            .Take(200)
            .ToList()
        : new List<string>();

    foreach (var f in fraFiles)
    {
        try
        {
            var pts = BodeCsvReader.Read(f, FraDataTypeRegistry.Get("AKM"));
            var m = BodeMetrics.Extract(pts, useCrossGuard: false);
            var g10 = FraHelpers.GainNearestAtHz(pts, 10.0);
            Line("FRA", "AKM", Name(f),
                $"pts={pts.Count} GM={N(m.GainMarginDb)} GMf={N(m.GmFreqHz)} PM={N(m.PhaseMarginDeg)} PMf={N(m.PmFreqHz)} PM3={N(m.PhaseMarginAtMinus3dBDeg)} PM3f={N(m.PmMinus3dBFreqHz)} G10={N(g10)}");
            okCount++;
        }
        catch (Exception ex) { Fail("FRA", "AKM", Name(f), ex); }
    }
}

// ── SCAN ─────────────────────────────────────────────────────────────────────
foreach (var (dtName, dir, inCand, outCand) in new[]
{
    ("AKM",      Path.Combine(dataRoot, @"AF\SCAN\AKM"),      "position code [dec]", "output"),
    ("robocube", Path.Combine(dataRoot, @"AF\SCAN\Robocube"), "Target",              "LaserDis"),
})
{
    var handler = ScanDataTypeRegistry.Get(dtName);
    foreach (var f in Files(dir))
    {
        try
        {
            var read = handler.Read(f, new[] { inCand }, new[] { outCand });
            // range 는 파일이 주는 힌트를 그대로 쓰되, 없으면 실제 입력 범위로
            var ranges = read.RangeHints.Count > 0
                ? read.RangeHints.Select(h => new ScanRangeDefinition(h.MinInput, h.MaxInput)).ToList()
                : new List<ScanRangeDefinition> { new(read.UpInput.Min(), read.UpInput.Max()) };

            var parts = new List<string> { $"up={read.UpInput.Count} down={read.DownInput.Count}" };
            foreach (var r in ranges)
            {
                var m = handler.ComputeMetrics(read, r);
                parts.Add($"[{N2(m.MinInput)}~{N2(m.MaxInput)} Hys={N2(m.Hys)} LI={N2(m.Linearity)}]");
            }
            Line("SCAN", dtName, Name(f), string.Join(" ", parts));
            okCount++;
        }
        catch (Exception ex) { Fail("SCAN", dtName, Name(f), ex); }
    }
}

// ── SETTLING ─────────────────────────────────────────────────────────────────
foreach (var (dtName, dir) in new[]
{
    ("Robocube", Path.Combine(dataRoot, @"AF\Settling\Robocube")),
    ("Robocube", Path.Combine(dataRoot, @"OIS\Settling\Robocube")),
    ("DW",       Path.Combine(dataRoot, @"OIS\Settling\DW")),
    ("Anosys",   Path.Combine(dataRoot, @"AF\Settling\Anosys")),
})
{
    var handler = SettlingDataTypeRegistry.Get(dtName);
    double judgeStart = handler.JudgeStartTimeMs;
    foreach (var f in Files(dir, handler.FileSearchPattern))
    {
        try
        {
            var series = handler.Read(f);
            var parts = new List<string> { $"series={series.Count}" };
            // 계열이 많으면 출력이 폭증하므로 앞 6개만 (파일 순서는 리더가 결정 — 결정적)
            foreach (var s in series.Take(6))
            {
                var r = SettlingMetrics.Compute(s.TimeMs, s.Value, s.ExplicitTarget,
                    toleranceBand: 100, tailFraction: 0.2, judgeStartTimeMs: judgeStart);
                parts.Add($"[{s.Label}|n={s.Value.Count} T={N(r.Target)} St={N(r.SettlingTimeMs)} Ov={N(r.Overshoot)}]");
            }
            Line("SETTLING", dtName, Name(f), string.Join(" ", parts));
            okCount++;
        }
        catch (Exception ex) { Fail("SETTLING", dtName, Name(f), ex); }
    }
}

// ── TILT ─────────────────────────────────────────────────────────────────────
{
    var handler = TiltDataTypeRegistry.Get("robocube");
    foreach (var f in Files(Path.Combine(dataRoot, @"AF\Tilt\Robocube")))
    {
        try
        {
            var read = handler.Read(f);
            var m = handler.ComputeMetrics(read);
            Line("TILT", "robocube", Name(f),
                $"up={read.UpTiltTx.Count} down={read.DownTiltTx.Count} PeakUp={N2(m.PeakTiltUp)} PeakDown={N2(m.PeakTiltDown)} Peak={N2(m.PeakTilt)}");
            okCount++;
        }
        catch (Exception ex) { Fail("TILT", "robocube", Name(f), ex); }
    }
}

// ── SINE WAVE ────────────────────────────────────────────────────────────────
foreach (var (dtName, dir) in new[]
{
    ("AKM", Path.Combine(dataRoot, @"OIS\Sinewave\AKM")),
    ("DW",  Path.Combine(dataRoot, @"OIS\Sinewave\DW")),
})
{
    var handler = SinewaveDataTypeRegistry.Get(dtName);
    foreach (var f in Files(dir))
    {
        try
        {
            var read = handler.Read(f);
            var m = handler.ComputeMetrics(read);
            Line("SINEWAVE", dtName, Name(f),
                $"n={read.TimeMs.Count} MaxErrX={N2(m.MaxErrX)} MaxErrY={N2(m.MaxErrY)}");
            okCount++;
        }
        catch (Exception ex) { Fail("SINEWAVE", dtName, Name(f), ex); }
    }
}

// ── CIRCLE TEST ──────────────────────────────────────────────────────────────
{
    var handler = CircleTestDataTypeRegistry.Get("AKM");
    foreach (var f in Files(Path.Combine(dataRoot, @"OIS\circletest\AKM")))
    {
        try
        {
            var read = handler.Read(f);
            var m = handler.ComputeMetrics(read);
            Line("CIRCLE", "AKM", Name(f),
                $"n={read.TimeMs.Count} MaxErrX={N2(m.MaxErrX)} MaxErrY={N2(m.MaxErrY)}");
            okCount++;
        }
        catch (Exception ex) { Fail("CIRCLE", "AKM", Name(f), ex); }
    }
}

// ── SQUARE TEST ──────────────────────────────────────────────────────────────
foreach (var (dtName, dir) in new[]
{
    ("AKM", Path.Combine(dataRoot, @"OIS\Sqaure\AKM")),
    ("DW",  Path.Combine(dataRoot, @"OIS\Sqaure\DW")),
})
{
    var handler = SquareTestDataTypeRegistry.Get(dtName);
    foreach (var f in Files(dir))
    {
        try
        {
            var read = handler.Read(f);
            var m = handler.ComputeMetrics(read, 5.0);   // 화면 기본값 5%
            Line("SQUARE", dtName, Name(f),
                $"axes={read.Axes.Count} St={N(m.MaxSettlingMs)} Ov={N(m.MaxOvershoot)} " +
                $"StUp={N(m.MaxSettlingUpMs)} OvUp={N(m.MaxOvershootUp)} StDn={N(m.MaxSettlingDownMs)} OvDn={N(m.MaxOvershootDown)}");
            okCount++;
        }
        catch (Exception ex) { Fail("SQUARE", dtName, Name(f), ex); }
    }
}

// ── DC SENSITIVITY ───────────────────────────────────────────────────────────
foreach (var (dtName, dir, inCand, outCand) in new[]
{
    ("AKM", Path.Combine(dataRoot, @"AF\DC\AKM"), "position code [dec]", "output"),
    ("DW",  Path.Combine(dataRoot, @"AF\DC\DW"),  "DAC",                 "Position_0"),
})
{
    var handler = DcSensitivityDataTypeRegistry.Get(dtName);
    foreach (var f in Files(dir))
    {
        try
        {
            var read = handler.Read(f, new[] { inCand }, new[] { outCand }, 30);
            Line("DC", dtName, Name(f),
                $"upIn={read.UpInput.Count} upOut={read.UpOutput.Count} downIn={read.DownInput.Count} downOut={read.DownOutput.Count} " +
                $"firstUp={N2(read.UpOutput.Count > 0 ? read.UpOutput[0] : double.NaN)} " +
                $"lastUp={N2(read.UpOutput.Count > 0 ? read.UpOutput[^1] : double.NaN)}");
            okCount++;
        }
        catch (Exception ex) { Fail("DC", dtName, Name(f), ex); }
    }
}

// ── 저장 ─────────────────────────────────────────────────────────────────────
var header = new StringBuilder();
header.AppendLine("# JahwaInspector.Light 계산 결과 기준선");
header.AppendLine("# 형식: 검사<TAB>데이터타입<TAB>파일명<TAB>지표");
header.AppendLine($"# 성공 {okCount}건 / 실패 {errCount}건");
header.AppendLine("# ※ 이 파일은 리팩터링 전후 비교용이며, 차이가 0이어야 한다.");
header.AppendLine();

outPath = Path.GetFullPath(outPath);
string? outDir = Path.GetDirectoryName(outPath);
if (!string.IsNullOrEmpty(outDir))
    Directory.CreateDirectory(outDir);
File.WriteAllText(outPath, header.ToString() + sb.ToString(), new UTF8Encoding(false));

Console.WriteLine($"기준선 저장: {outPath}");
Console.WriteLine($"  성공 {okCount}건 / 실패 {errCount}건");
Console.WriteLine($"  줄 수 {File.ReadAllLines(outPath).Length}");
return 0;
