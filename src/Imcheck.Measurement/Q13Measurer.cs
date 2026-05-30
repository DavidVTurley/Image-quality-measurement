using OpenCvSharp;

namespace Imcheck.Measurement;

public sealed class Q13Measurer
{
    public Q13MeasurementResult Measure(string imagePath, Q13MeasurementOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        options ??= new Q13MeasurementOptions();
        ValidateOptions(options);

        using var image = Cv2.ImRead(imagePath, ImreadModes.Unchanged);
        if (image.Empty())
        {
            throw new InvalidOperationException($"Unable to load image: {imagePath}");
        }

        if (image.Depth() != MatType.CV_8U)
        {
            throw new NotSupportedException("Only 8-bit images are supported in the reference-first implementation.");
        }

        var channels = image.Channels();
        if (channels is not (1 or 3 or 4))
        {
            throw new NotSupportedException($"Unsupported channel count: {channels}.");
        }

        var sampleSize = options.SampleSize;
        var patches = new List<PatchMeasurement>(options.PatchCount);
        var targets = Q13Target.KodakPatches;

        for (var patchIndex = 0; patchIndex < options.PatchCount; patchIndex++)
        {
            var target = targets[patchIndex];
            var rect = GetCenteredPatchSample(image.Width, image.Height, options.PatchCount, patchIndex, sampleSize);
            using var roi = new Mat(image, rect);
            patches.Add(MeasurePatch(roi, target, patchIndex, channels));
        }

        return new Q13MeasurementResult(
            imagePath,
            EstimateSamplingPixelsPerInch(image.Width, options.PatchCount),
            sampleSize,
            CalculateInverseGamma(patches, channel: 0),
            CalculateInverseGamma(patches, channel: 1),
            CalculateInverseGamma(patches, channel: 2),
            patches);
    }

    private static void ValidateOptions(Q13MeasurementOptions options)
    {
        if (options.PatchCount != Q13Target.KodakPatches.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Only the 20-patch Kodak Q-13 target is supported.");
        }

        if (options.SampleSize <= 0 || options.SampleSize % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Sample size must be a positive odd number.");
        }
    }

    private static Rect GetCenteredPatchSample(int width, int height, int patchCount, int patchIndex, int sampleSize)
    {
        var patchWidth = width / (double)patchCount;
        var centerX = (patchIndex + 0.5) * patchWidth;
        var centerY = height / 2.0;
        var x = Clamp((int)Math.Round(centerX - sampleSize / 2.0), 0, width - sampleSize);
        var y = Clamp((int)Math.Round(centerY - sampleSize / 2.0), 0, height - sampleSize);

        return new Rect(x, y, sampleSize, sampleSize);
    }

    private static PatchMeasurement MeasurePatch(Mat roi, Q13TargetPatch target, int patchIndex, int channels)
    {
        if (channels == 1)
        {
            var (mean, noise) = MeanAndPopulationStdDev(roi);
            return new PatchMeasurement(
                patchIndex,
                target.InputRed,
                target.InputGreen,
                target.InputBlue,
                mean,
                mean,
                mean,
                noise,
                noise,
                noise,
                IsColor: false);
        }

        Cv2.Split(roi, out var splitChannels);
        try
        {
            var (blueMean, blueNoise) = MeanAndPopulationStdDev(splitChannels[0]);
            var (greenMean, greenNoise) = MeanAndPopulationStdDev(splitChannels[1]);
            var (redMean, redNoise) = MeanAndPopulationStdDev(splitChannels[2]);

            return new PatchMeasurement(
                patchIndex,
                target.InputRed,
                target.InputGreen,
                target.InputBlue,
                redMean,
                greenMean,
                blueMean,
                redNoise,
                greenNoise,
                blueNoise,
                IsColor: true);
        }
        finally
        {
            foreach (var channel in splitChannels)
            {
                channel.Dispose();
            }
        }
    }

    private static (double Mean, double StdDev) MeanAndPopulationStdDev(Mat mat)
    {
        Cv2.MeanStdDev(mat, out var mean, out var stdDev);
        return (mean.Val0, stdDev.Val0);
    }

    private static double CalculateInverseGamma(IReadOnlyList<PatchMeasurement> patches, int channel)
    {
        // Imcheck's Q-13 reference output matches a highlight-region fit over patches A through 6.
        var fitCount = Math.Min(7, patches.Count);
        var points = patches.Take(fitCount)
            .Select(p => (X: Math.Log(InputForChannel(Q13Target.KodakPatches[p.Index], channel)), Y: Math.Log(Math.Max(OutputForChannel(p, channel) / 255.0, double.Epsilon))))
            .ToArray();

        var averageX = points.Average(p => p.X);
        var averageY = points.Average(p => p.Y);
        var numerator = points.Sum(p => (p.X - averageX) * (p.Y - averageY));
        var denominator = points.Sum(p => Math.Pow(p.X - averageX, 2));

        if (denominator == 0)
        {
            return double.NaN;
        }

        var gamma = numerator / denominator;
        return 1.0 / gamma;
    }

    private static double InputForChannel(Q13TargetPatch patch, int channel)
    {
        return channel switch
        {
            0 => patch.InputRed,
            1 => patch.InputGreen,
            _ => patch.InputBlue
        };
    }

    private static double OutputForChannel(PatchMeasurement patch, int channel)
    {
        return channel switch
        {
            0 => patch.OutputRed,
            1 => patch.OutputGreen,
            _ => patch.OutputBlue
        };
    }

    private static double EstimateSamplingPixelsPerInch(int imageWidth, int patchCount)
    {
        // Kodak Q-13 gray scale is nominally 8 inches wide.
        return imageWidth / 8.0 * 300.0 / 297.0;
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
