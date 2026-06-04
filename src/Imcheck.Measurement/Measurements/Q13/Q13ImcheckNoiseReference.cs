using System.Globalization;

namespace Imcheck.Measurement.Measurements.Q13;

public sealed record Q13ImcheckNoiseReference(IReadOnlyList<Q13ImcheckNoiseReferenceRow> Rows)
{
    public static Q13ImcheckNoiseReference Load(string path, int patchCount = 20)
    {
        var lines = File.ReadAllLines(path);
        var headerIndex = Array.FindIndex(lines, line => line.StartsWith("R\tG\tB\tLum", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
        {
            throw new InvalidDataException("ImCheck Q13 noise reference is missing the R/G/B table header.");
        }

        var rows = new List<Q13ImcheckNoiseReferenceRow>();
        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var parts = lines[i].Split('\t');
            if (parts.Length < 12)
            {
                continue;
            }

            rows.Add(new Q13ImcheckNoiseReferenceRow(
                rows.Count,
                Parse(parts[0]),
                Parse(parts[1]),
                Parse(parts[2]),
                Parse(parts[4]),
                Parse(parts[5]),
                Parse(parts[6])));
        }

        if (rows.Count != patchCount)
        {
            throw new InvalidDataException($"Expected {patchCount} ImCheck Q13 noise rows, but found {rows.Count}.");
        }

        return new Q13ImcheckNoiseReference(rows);
    }

    public Q13ImcheckNoiseComparison Compare(Q13MeasurementResult result)
    {
        if (result.Patches.Count != Rows.Count)
        {
            throw new ArgumentException("Measured Q13 patch count does not match the ImCheck reference.", nameof(result));
        }

        var meanError = 0.0;
        var noiseError = 0.0;
        var maxError = 0.0;
        var maxErrorPatch = 0;

        for (var i = 0; i < Rows.Count; i++)
        {
            var reference = Rows[i];
            var patch = result.Patches[i];
            var differences = new[]
            {
                Math.Abs(patch.OutputRed - reference.Red),
                Math.Abs(patch.OutputGreen - reference.Green),
                Math.Abs(patch.OutputBlue - reference.Blue),
                Math.Abs(patch.NoiseRed - reference.NoiseRed),
                Math.Abs(patch.NoiseGreen - reference.NoiseGreen),
                Math.Abs(patch.NoiseBlue - reference.NoiseBlue)
            };

            meanError += differences[0] + differences[1] + differences[2];
            noiseError += differences[3] + differences[4] + differences[5];

            var localMax = differences.Max();
            if (localMax > maxError)
            {
                maxError = localMax;
                maxErrorPatch = i;
            }
        }

        var divisor = Rows.Count * 3.0;
        return new Q13ImcheckNoiseComparison(
            meanError / divisor,
            noiseError / divisor,
            (meanError + noiseError) / (divisor * 2.0),
            maxError,
            maxErrorPatch);
    }

    private static double Parse(string value)
    {
        return double.Parse(
            value.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
    }
}

public sealed record Q13ImcheckNoiseReferenceRow(
    int PatchIndex,
    double Red,
    double Green,
    double Blue,
    double NoiseRed,
    double NoiseGreen,
    double NoiseBlue);

public sealed record Q13ImcheckNoiseComparison(
    double AverageMeanError,
    double AverageNoiseError,
    double CombinedAverageError,
    double MaxError,
    int MaxErrorPatch);
