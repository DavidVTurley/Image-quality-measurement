using Imcheck.Measurement.Measurements.Common;

namespace Imcheck.Measurement.Measurements.Q13;

public static class Q13SamplePointCsv
{
    public static IReadOnlyList<Q13SamplePoint> Load(string path, int patchCount = 20)
    {
        using var reader = new StreamReader(path);
        var lineNumber = 0;
        string? headerLine = null;

        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            headerLine = rawLine;
            break;
        }

        if (headerLine is null)
        {
            throw new InvalidDataException("Q13 points CSV is empty.");
        }

        var headers = CsvTable.SplitLine(headerLine);
        var hasPatchColumn = headers.Any(header => string.Equals(header, "Patch", StringComparison.OrdinalIgnoreCase));
        var indexes = CsvTable.RequiredIndexes(
            headers,
            hasPatchColumn ? ["Patch", "X", "Y"] : ["X", "Y"],
            "Q13 points CSV");

        var points = new List<Q13SamplePoint>();
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parts = CsvTable.SplitLine(rawLine);
            var patchIndex = hasPatchColumn
                ? CsvTable.ParseInt(parts, indexes["Patch"], lineNumber, "Patch")
                : points.Count;
            var x = CsvTable.ParseDouble(parts, indexes["X"], lineNumber, "X");
            var y = CsvTable.ParseDouble(parts, indexes["Y"], lineNumber, "Y");
            points.Add(new Q13SamplePoint(patchIndex, x, y));
        }

        if (points.Count != patchCount)
        {
            throw new InvalidDataException($"Expected {patchCount} Q13 sample points, but found {points.Count}.");
        }

        var expected = Enumerable.Range(0, patchCount).ToArray();
        var actual = points.Select(point => point.PatchIndex).Order().ToArray();
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException($"Q13 sample points must use patch indexes 0 through {patchCount - 1} exactly once.");
        }

        return points.OrderBy(point => point.PatchIndex).ToArray();
    }
}
