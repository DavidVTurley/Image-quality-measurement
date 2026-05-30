using System.Globalization;
using System.Text.RegularExpressions;

namespace Imcheck.Measurement.Meaasurements.Q13;

public static class Q13SamplePointCsv
{
    public static IReadOnlyList<Q13SamplePoint> Load(string path, int patchCount = 20)
    {
        var points = new List<Q13SamplePoint>();
        var lineNumber = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = Regex.Split(line, @"[,\t; ]+")
                .Where(part => part.Length > 0)
                .ToArray();

            if (parts.Length < 2)
            {
                throw new InvalidDataException($"Line {lineNumber} must contain either X,Y or Patch,X,Y.");
            }

            if (!TryParsePoint(parts, points.Count, out var point))
            {
                if (lineNumber == 1)
                {
                    continue;
                }

                throw new InvalidDataException($"Line {lineNumber} could not be parsed as X,Y or Patch,X,Y.");
            }

            points.Add(point);
        }

        if (points.Count != patchCount)
        {
            throw new InvalidDataException($"Expected {patchCount} sample points, but found {points.Count}.");
        }

        var expected = Enumerable.Range(0, patchCount).ToArray();
        var actual = points.Select(point => point.PatchIndex).Order().ToArray();
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException($"Sample point patch indexes must be 0 through {patchCount - 1} exactly once.");
        }

        return points.OrderBy(point => point.PatchIndex).ToArray();
    }

    private static bool TryParsePoint(string[] parts, int implicitIndex, out Q13SamplePoint point)
    {
        point = new Q13SamplePoint(0, 0, 0);

        if (parts.Length >= 3 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var patchIndex) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var explicitX) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var explicitY))
        {
            point = new Q13SamplePoint(patchIndex, explicitX, explicitY);
            return true;
        }

        if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            point = new Q13SamplePoint(implicitIndex, x, y);
            return true;
        }

        return false;
    }
}
