using System.Globalization;
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

        var headers = SplitCsvLine(headerLine);
        var indexes = RequiredIndexes(headers,
        [
            "Name",
            "SampleTopLeftX",
            "SampleTopLeftY",
            "SampleTopRightX",
            "SampleTopRightY",
            "SampleBottomRightX",
            "SampleBottomRightY",
            "SampleBottomLeftX",
            "SampleBottomLeftY"
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
            var topLeftX = ParseDouble(parts, indexes["SampleTopLeftX"], lineNumber, "SampleTopLeftX");
            var topLeftY = ParseDouble(parts, indexes["SampleTopLeftY"], lineNumber, "SampleTopLeftY");
            var topRightX = ParseDouble(parts, indexes["SampleTopRightX"], lineNumber, "SampleTopRightX");
            var topRightY = ParseDouble(parts, indexes["SampleTopRightY"], lineNumber, "SampleTopRightY");
            var bottomRightX = ParseDouble(parts, indexes["SampleBottomRightX"], lineNumber, "SampleBottomRightX");
            var bottomRightY = ParseDouble(parts, indexes["SampleBottomRightY"], lineNumber, "SampleBottomRightY");
            var bottomLeftX = ParseDouble(parts, indexes["SampleBottomLeftX"], lineNumber, "SampleBottomLeftX");
            var bottomLeftY = ParseDouble(parts, indexes["SampleBottomLeftY"], lineNumber, "SampleBottomLeftY");

            var centerX = (topLeftX + topRightX + bottomRightX + bottomLeftX) / 4.0;
            var centerY = (topLeftY + topRightY + bottomRightY + bottomLeftY) / 4.0;
            var sampleSize = MeasurementMath.MakeOdd((int)Math.Round(((topRightX - topLeftX) + (bottomRightX - bottomLeftX) + (bottomLeftY - topLeftY) + (bottomRightY - topRightY)) / 4.0));

            samples.Add(new UniformitySampleLocation(name, centerX, centerY, sampleSize));
        }

        if (samples.Count == 0)
        {
            throw new InvalidDataException("Uniformity results CSV does not contain any sample rows.");
        }

        return samples;
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

    private static double ParseDouble(IReadOnlyList<string> parts, int index, int lineNumber, string column)
    {
        if (index >= parts.Count || !double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
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
