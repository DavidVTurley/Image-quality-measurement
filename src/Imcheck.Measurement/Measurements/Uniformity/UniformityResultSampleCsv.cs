using System.Globalization;

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

        var headers = SplitCsvLine(headerLine);
        var indexes = RequiredIndexes(headers,
        [
            "Name",
            "SampleCenterX",
            "SampleCenterY",
            "SampleWidth",
            "SampleHeight"
        ]);

        var samples = new List<UniformitySampleLocation>();
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parts = SplitCsvLine(rawLine);
            var name = ParseString(parts, indexes["Name"], lineNumber, "Name");
            var centerX = ParseInt(parts, indexes["SampleCenterX"], lineNumber, "SampleCenterX");
            var centerY = ParseInt(parts, indexes["SampleCenterY"], lineNumber, "SampleCenterY");
            var width = ParseInt(parts, indexes["SampleWidth"], lineNumber, "SampleWidth");
            var height = ParseInt(parts, indexes["SampleHeight"], lineNumber, "SampleHeight");
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

    private static Dictionary<string, int> RequiredIndexes(IReadOnlyList<string> headers, IReadOnlyList<string> requiredHeaders)
    {
        var indexes = headers
            .Select((header, index) => (Header: header.Trim(), Index: index))
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var requiredHeader in requiredHeaders)
        {
            if (!indexes.ContainsKey(requiredHeader))
            {
                throw new InvalidDataException($"Uniformity results CSV is missing the '{requiredHeader}' column.");
            }
        }

        return indexes;
    }

    private static string ParseString(IReadOnlyList<string> parts, int index, int lineNumber, string column)
    {
        if (index >= parts.Count || string.IsNullOrWhiteSpace(parts[index]))
        {
            throw new InvalidDataException($"Line {lineNumber} has an invalid '{column}' value.");
        }

        return parts[index].Trim();
    }

    private static int ParseInt(IReadOnlyList<string> parts, int index, int lineNumber, string column)
    {
        if (index >= parts.Count || !int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException($"Line {lineNumber} has an invalid '{column}' value.");
        }

        return value;
    }

    private static IReadOnlyList<string> SplitCsvLine(string line)
    {
        return line.Split(',').Select(part => part.Trim()).ToArray();
    }
}
