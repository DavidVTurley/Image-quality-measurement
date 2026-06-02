using Imcheck.Measurement.Measurements.Common;

namespace Imcheck.Measurement.Measurements.Q13;

public static class Q13ResultSampleCsv
{
    public static Q13ImportedSamples Load(string path, int patchCount = 20)
    {
        var samples = LoadRows(path, patchCount);
        var normalizedSize = NormalizeSampleSize(samples.Min(sample => Math.Min(sample.Width, sample.Height)));
        var centers = samples
            .OrderBy(sample => sample.PatchIndex)
            .Select(sample => new Q13SamplePoint(sample.PatchIndex, sample.CenterX, sample.CenterY))
            .ToArray();

        return new Q13ImportedSamples(centers, normalizedSize);
    }

    public static IReadOnlyList<Q13SamplePoint> LoadSampleCenters(string path, int patchCount = 20)
    {
        return Load(path, patchCount).Centers;
    }

    private static IReadOnlyList<Q13ImportedSampleRow> LoadRows(string path, int patchCount)
    {
        using var reader = new StreamReader(path);
        string? headerLine = null;
        var lineNumber = 0;

        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (rawLine.StartsWith("Patch,", StringComparison.OrdinalIgnoreCase))
            {
                headerLine = rawLine;
                break;
            }
        }

        if (headerLine is null)
        {
            throw new InvalidDataException("Q13 results CSV is missing the patch table header.");
        }

        var headers = CsvTable.SplitLine(headerLine);
        var indexes = CsvTable.RequiredIndexes(headers,
        [
            "Patch",
            "SampleCenterX",
            "SampleCenterY",
            "SampleWidth",
            "SampleHeight"
        ], "Q13 results CSV");

        var samples = new List<Q13ImportedSampleRow>();
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parts = CsvTable.SplitLine(rawLine);
            var patchIndex = CsvTable.ParseInt(parts, indexes["Patch"], lineNumber, "Patch");
            var centerX = CsvTable.ParseInt(parts, indexes["SampleCenterX"], lineNumber, "SampleCenterX");
            var centerY = CsvTable.ParseInt(parts, indexes["SampleCenterY"], lineNumber, "SampleCenterY");
            var width = CsvTable.ParseInt(parts, indexes["SampleWidth"], lineNumber, "SampleWidth");
            var height = CsvTable.ParseInt(parts, indexes["SampleHeight"], lineNumber, "SampleHeight");

            samples.Add(new Q13ImportedSampleRow(patchIndex, centerX, centerY, width, height));
        }

        if (samples.Count != patchCount)
        {
            throw new InvalidDataException($"Expected {patchCount} Q13 sample rows, but found {samples.Count}.");
        }

        var expected = Enumerable.Range(0, patchCount).ToArray();
        var actual = samples.Select(sample => sample.PatchIndex).Order().ToArray();
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException($"Q13 sample rows must use patch indexes 0 through {patchCount - 1} exactly once.");
        }

        return samples.OrderBy(sample => sample.PatchIndex).ToArray();
    }

    private static int NormalizeSampleSize(int value)
    {
        if (value <= 0)
        {
            throw new InvalidDataException("Q13 sample width and height must be positive integers.");
        }

        return value % 2 == 0 ? value + 1 : value;
    }
}

public sealed record Q13ImportedSamples(IReadOnlyList<Q13SamplePoint> Centers, int SampleSize);

internal sealed record Q13ImportedSampleRow(int PatchIndex, int CenterX, int CenterY, int Width, int Height);
