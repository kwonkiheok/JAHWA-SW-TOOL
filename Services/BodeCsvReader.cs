using System.Globalization;
using System.IO;

namespace JahwaInspector.Light.Services;

public sealed record BodePoint(double FrequencyHz, double GainDb, double PhaseDeg);

public static class BodeCsvReader
{
    public static List<BodePoint> Read(string csvPath, FraDataType dataType)
    {
        var lines = File.ReadAllLines(csvPath);
        if (lines.Length == 0) return [];

        int first = 0;
        while (first < lines.Length && string.IsNullOrWhiteSpace(lines[first])) first++;
        if (first >= lines.Length) return [];

        char d = ','; // confirmed

        var firstCells = Split(lines[first], d);
        bool headerless = IsNumericRow(firstCells);

        if (headerless)
            return Parse(lines, d, first, 0, 1, 2);

        var headers = firstCells.Select(Norm).ToArray();
        var definition = FraDataTypeRegistry.Get(dataType);
        int fi = Find(headers, definition.FrequencyHeaders);
        int gi = Find(headers, definition.GainHeaders);
        int pi = Find(headers, definition.PhaseHeaders);

        if (fi < 0 || gi < 0 || pi < 0) { fi = 0; gi = 1; pi = 2; }

        return Parse(lines, d, first + 1, fi, gi, pi);
    }

    private static List<BodePoint> Parse(string[] lines, char d, int start, int fi, int gi, int pi)
    {
        var list = new List<BodePoint>();
        for (int i = start; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var c = Split(lines[i], d);
            int need = Math.Max(fi, Math.Max(gi, pi));
            if (c.Count <= need) continue;

            if (!TryD(c[fi], out var f)) continue;
            if (!TryD(c[gi], out var g)) continue;
            if (!TryD(c[pi], out var p)) continue;

            list.Add(new BodePoint(f, g, p));
        }

        return list
            .OrderBy(x => x.FrequencyHz)
            .ToList();
    }

    private static bool IsNumericRow(IReadOnlyList<string> c)
        => c.Count >= 3 && TryD(c[0], out _) && TryD(c[1], out _) && TryD(c[2], out _);

    private static int Find(string[] headers, IReadOnlyList<string> candidates)
    {
        foreach (var s in candidates)
        {
            var ns = Norm(s);
            for (int i = 0; i < headers.Length; i++)
                if (headers[i] == ns) return i;
        }
        return -1;
    }

    private static bool TryD(string s, out double v)
    {
        s = (s ?? "").Trim().Replace(",", "");
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v);
    }

    private static string Norm(string s)
        => (s ?? "").Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace("[", "").Replace("]", "")
            .Replace("(", "").Replace(")", "")
            .Replace("°", "deg");

    private static List<string> Split(string line, char d) => line.Split(d).Select(x => x.Trim()).ToList();
}
