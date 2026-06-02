using System.Globalization;
using System.Text;

namespace Imcheck.Measurement.Measurements.Common;

internal static class CsvTable
{
    public static IReadOnlyList<string> SplitLine(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var value = line[i];
            if (value == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (value == ',' && !inQuotes)
            {
                values.Add(builder.ToString().Trim());
                builder.Clear();
                continue;
            }

            builder.Append(value);
        }

        values.Add(builder.ToString().Trim());
        return values;
    }

    public static Dictionary<string, int> RequiredIndexes(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> requiredHeaders,
        string sourceName)
    {
        var indexes = headers
            .Select((header, index) => (Header: header.Trim(), Index: index))
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var requiredHeader in requiredHeaders)
        {
            if (!indexes.ContainsKey(requiredHeader))
            {
                throw new InvalidDataException($"{sourceName} is missing the '{requiredHeader}' column.");
            }
        }

        return indexes;
    }

    public static string ParseString(IReadOnlyList<string> parts, int index, int lineNumber, string column)
    {
        if (index >= parts.Count || string.IsNullOrWhiteSpace(parts[index]))
        {
            throw new InvalidDataException($"Line {lineNumber} has an invalid '{column}' value.");
        }

        return parts[index].Trim();
    }

    public static int ParseInt(IReadOnlyList<string> parts, int index, int lineNumber, string column)
    {
        if (index >= parts.Count || !int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException($"Line {lineNumber} has an invalid '{column}' value.");
        }

        return value;
    }

    public static double ParseDouble(IReadOnlyList<string> parts, int index, int lineNumber, string column)
    {
        if (index >= parts.Count || !double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException($"Line {lineNumber} has an invalid '{column}' value.");
        }

        return value;
    }

    public static string Format(double value, string format = "0.####")
    {
        return double.IsFinite(value)
            ? value.ToString(format, CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public static void AppendRow(StringBuilder builder, params object?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendValue(builder, values[i]);
        }

        builder.AppendLine();
    }

    private static void AppendValue(StringBuilder builder, object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            double number => Format(number),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        if (text.Contains(',') || text.Contains('"') || text.Contains('\r') || text.Contains('\n'))
        {
            builder.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
            return;
        }

        builder.Append(text);
    }
}
