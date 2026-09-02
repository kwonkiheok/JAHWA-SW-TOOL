using System.IO;

namespace MeasureHub.Services;

public sealed class TiltReadResult
{
    public int HeaderRow { get; init; }
    public IReadOnlyList<double> UpTiltTx { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> UpTiltTy { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DownTiltTx { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> DownTiltTy { get; init; } = Array.Empty<double>();
}

public static class RobocubeTiltReader
{
    public static TiltReadResult Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            throw new InvalidOperationException("Empty robocube file.");

        int upHeader = RobocubeScanReader.FindSectionHeader(lines, "(Up)", "Target", "LaserDis", "TiltTx", "TiltTy");
        int downHeader = RobocubeScanReader.FindSectionHeader(lines, "(Down)", "Target", "LaserDis", "TiltTx", "TiltTy");
        if (upHeader < 0 || downHeader < 0)
            throw new InvalidOperationException("robocube Up/Down sections were not found.");

        var upHeaderCells = RobocubeScanReader.Split(lines[upHeader]);
        var downHeaderCells = RobocubeScanReader.Split(lines[downHeader]);
        int upTxIndex = RobocubeScanReader.FindColumn(upHeaderCells, "TiltTx");
        int upTyIndex = RobocubeScanReader.FindColumn(upHeaderCells, "TiltTy");
        int downTxIndex = RobocubeScanReader.FindColumn(downHeaderCells, "TiltTx");
        int downTyIndex = RobocubeScanReader.FindColumn(downHeaderCells, "TiltTy");

        if (upTxIndex < 0 || upTyIndex < 0 || downTxIndex < 0 || downTyIndex < 0)
            throw new InvalidOperationException("robocube TiltTx/TiltTy columns were not found.");

        var (upTx, upTy) = RobocubeScanReader.ReadSection(lines, upHeader + 1, downHeader, upTxIndex, upTyIndex);
        var (downTx, downTy) = RobocubeScanReader.ReadSection(lines, downHeader + 1, lines.Length, downTxIndex, downTyIndex);
        if (upTx.Count < 2 || downTx.Count < 2)
            throw new InvalidOperationException("Not enough valid robocube TILT data rows.");

        return new TiltReadResult
        {
            HeaderRow = upHeader,
            UpTiltTx = upTx,
            UpTiltTy = upTy,
            DownTiltTx = downTx,
            DownTiltTy = downTy,
        };
    }
}
