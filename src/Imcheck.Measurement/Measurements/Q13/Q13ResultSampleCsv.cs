using System.Globalization;

namespace Imcheck.Measurement.Measurements.Q13;

public static class Q13ResultSampleCsv
{
    public static IReadOnlyList<Q13SamplePoint> LoadSampleCenters(string path, int patchCount = 20)
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

        var headers = SplitCsvLine(headerLine);
        var indexes = RequiredIndexes(headers,
        [
            "Patch",
            "SampleTopLeftX",
            "SampleTopLeftY",
            "SampleTopRightX",
            "SampleTopRightY",
            "SampleBottomRightX",
            "SampleBottomRightY",
            "SampleBottomLeftX",
            "SampleBottomLeftY"
        ]);

        var points = new List<Q13SamplePoint>();
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parts = SplitCsvLine(rawLine);
            var patchIndex = ParseInt(parts, indexes["Patch"], lineNumber, "Patch");
            var centerX = (
                ParseDouble(parts, indexes["SampleTopLeftX"], lineNumber, "SampleTopLeftX") +
                ParseDouble(parts, indexes["SampleTopRightX"], lineNumber, "SampleTopRightX") +
                ParseDouble(parts, indexes["SampleBottomRightX"], lineNumber, "SampleBottomRightX") +
                ParseDouble(parts, indexes["SampleBottomLeftX"], lineNumber, "SampleBottomLeftX")) / 4.0;
            var centerY = (
                ParseDouble(parts, indexes["SampleTopLeftY"], lineNumber, "SampleTopLeftY") +
                ParseDouble(parts, indexes["SampleTopRightY"], lineNumber, "SampleTopRightY") +
                ParseDouble(parts, indexes["SampleBottomRightY"], lineNumber, "SampleBottomRightY") +
                ParseDouble(parts, indexes["SampleBottomLeftY"], lineNumber, "SampleBottomLeftY")) / 4.0;

            points.Add(new Q13SamplePoint(patchIndex, centerX, centerY));
        }

        if (points.Count != patchCount)
        {
            throw new InvalidDataException($"Expected {patchCount} Q13 sample rows, but found {points.Count}.");
        }

        var expected = Enumerable.Range(0, patchCount).ToArray();
        var actual = points.Select(point => point.PatchIndex).Order().ToArray();
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException($"Q13 sample rows must use patch indexes 0 through {patchCount - 1} exactly once.");
        }

        return points.OrderBy(point => point.PatchIndex).ToArray();
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
                throw new InvalidDataException($"Q13 results CSV is missing the '{requiredHeader}' column.");
            }
        }

        return indexes;
    }

    private static int ParseInt(IReadOnlyList<string> parts, int index, int lineNumber, string column)
    {
        if (index >= parts.Count || !int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException($"Line {lineNumber} has an invalid '{column}' value.");
        }

        return value;
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
