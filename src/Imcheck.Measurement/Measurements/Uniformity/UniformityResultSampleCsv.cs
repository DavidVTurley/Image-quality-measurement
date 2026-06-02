using Imcheck.Measurement.Measurements.Common;

namespace Imcheck.Measurement.Measurements.Uniformity;

public static class UniformityResultSampleCsv
{
    public static IReadOnlyList<UniformitySampleLocation> LoadSamples(string path)
    {
        using var reader = new StreamReader(path);
        string? headerLine = null;
        var lineNumber = 0;

        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (rawLine.StartsWith("Name,", StringComparison.OrdinalIgnoreCase))
            {
                headerLine = rawLine;
                break;
            }
        }

        if (headerLine is null)
        {
            throw new InvalidDataException("Uniformity results CSV is missing the sample table header.");
        }

        var headers = CsvTable.SplitLine(headerLine);
        var indexes = CsvTable.RequiredIndexes(headers,
        [
            "Name",
            "SampleCenterX",
            "SampleCenterY",
            "SampleWidth",
            "SampleHeight"
        ], "Uniformity results CSV");

        var samples = new List<UniformitySampleLocation>();
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parts = CsvTable.SplitLine(rawLine);
            var name = CsvTable.ParseString(parts, indexes["Name"], lineNumber, "Name");
            var centerX = CsvTable.ParseInt(parts, indexes["SampleCenterX"], lineNumber, "SampleCenterX");
            var centerY = CsvTable.ParseInt(parts, indexes["SampleCenterY"], lineNumber, "SampleCenterY");
            var width = CsvTable.ParseInt(parts, indexes["SampleWidth"], lineNumber, "SampleWidth");
            var height = CsvTable.ParseInt(parts, indexes["SampleHeight"], lineNumber, "SampleHeight");
            var sampleSize = NormalizeSampleSize(Math.Min(width, height));

            samples.Add(new UniformitySampleLocation(name, centerX, centerY, sampleSize));
        }

        if (samples.Count == 0)
        {
            throw new InvalidDataException("Uniformity results CSV does not contain any sample rows.");
        }

        return samples;
    }

    private static int NormalizeSampleSize(int value)
    {
        if (value <= 0)
        {
            throw new InvalidDataException("Uniformity sample width and height must be positive integers.");
        }

        return value % 2 == 0 ? value + 1 : value;
    }
}
