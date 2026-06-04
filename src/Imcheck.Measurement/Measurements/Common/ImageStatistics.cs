using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Common;

internal static class ImageStatistics
{
    public static (double Mean, double StdDev) MeanAndPopulationStdDev(Mat mat)
    {
        Cv2.MeanStdDev(mat, out var mean, out var stdDev);
        return (mean.Val0, stdDev.Val0);
    }

    public static (double Mean, double StdDev, int Rejected, int Total) MeanAndPopulationStdDevWithSigmaClipping(Mat mat, double sigmaThreshold)
    {
        if (sigmaThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigmaThreshold), "Sigma threshold must be positive.");
        }

        var values = new double[mat.Width * mat.Height];
        var index = 0;
        for (var y = 0; y < mat.Height; y++)
        {
            for (var x = 0; x < mat.Width; x++)
            {
                values[index++] = mat.At<byte>(y, x);
            }
        }

        var keep = Enumerable.Repeat(true, values.Length).ToArray();
        var changed = true;
        var pass = 0;

        while (changed && pass++ < 10)
        {
            changed = false;
            var (mean, stdDev) = MeanAndPopulationStdDev(values, keep);
            if (stdDev <= 0)
            {
                break;
            }

            var limit = sigmaThreshold * stdDev;
            for (var i = 0; i < values.Length; i++)
            {
                if (keep[i] && Math.Abs(values[i] - mean) > limit)
                {
                    keep[i] = false;
                    changed = true;
                }
            }
        }

        var final = MeanAndPopulationStdDev(values, keep);
        return (final.Mean, final.StdDev, keep.Count(include => !include), values.Length);
    }

    private static (double Mean, double StdDev) MeanAndPopulationStdDev(IReadOnlyList<double> values, IReadOnlyList<bool> keep)
    {
        var count = 0;
        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (!keep[i])
            {
                continue;
            }

            count++;
            sum += values[i];
        }

        if (count == 0)
        {
            return (double.NaN, double.NaN);
        }

        var mean = sum / count;
        var sumSquares = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (keep[i])
            {
                sumSquares += Math.Pow(values[i] - mean, 2);
            }
        }

        return (mean, Math.Sqrt(sumSquares / count));
    }
}
