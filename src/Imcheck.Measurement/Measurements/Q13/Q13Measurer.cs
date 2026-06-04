using Imcheck.Measurement.Measurements.Common;
using Imcheck.Measurement.Measurements;
using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Q13;

public sealed class Q13Measurer : IImageMeasurer<Q13MeasurementOptions, Q13MeasurementResult>
{
    public Q13MeasurementResult Measure(string imagePath, Q13MeasurementOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        options ??= new Q13MeasurementOptions();
        ValidateOptions(options);

        using var image = ImageValidation.LoadRequired(imagePath, ImreadModes.Unchanged, "Q13 measurement");
        var channels = ImageValidation.RequireEightBitChannels(image, "reference-first");

        var sampleSize = options.SampleSize;
        var patches = new List<PatchMeasurement>(options.PatchCount);

        if (options.StripGeometry is not null)
        {
            return MeasureStripGeometry(image, imagePath, channels, options);
        }

        var sampleCenters = ResolveSampleCenters(image.Width, image.Height, options);

        for (var patchIndex = 0; patchIndex < options.PatchCount; patchIndex++)
        {
            var center = sampleCenters[patchIndex];
            patches.Add(MeasurePatch(image, patchIndex, channels, sampleSize, center.X, center.Y, options.UseOutlierRejection, options.OutlierSigmaThreshold));
        }

        return new Q13MeasurementResult(
            imagePath,
            EstimateSamplingPixelsPerInch(sampleCenters, image.Width, options.PatchCount),
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

        if (options.OutlierSigmaThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Outlier sigma threshold must be positive.");
        }

        if (options.SampleCenters is not null && options.SampleCenters.Count != options.PatchCount)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Explicit sample centers must include exactly one point per Q-13 patch.");
        }

        if (options.SampleCenters is not null && options.StripGeometry is not null)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Use either explicit sample centers or strip geometry, not both.");
        }

        if (options.SampleRegions is not null && options.StripGeometry is null)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Sample regions require strip geometry.");
        }

        if (options.SampleRegions is not null && options.SampleRegions.Count != options.PatchCount)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Explicit sample regions must include exactly one region per Q-13 patch.");
        }

        if (options.SampleCenters is not null)
        {
            var expected = Enumerable.Range(0, options.PatchCount).ToArray();
            var actual = options.SampleCenters.Select(point => point.PatchIndex).Order().ToArray();
            if (!actual.SequenceEqual(expected))
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Explicit sample centers must be indexed 0 through 19 exactly once.");
            }
        }

        if (options.SampleRegions is not null)
        {
            var expected = Enumerable.Range(0, options.PatchCount).ToArray();
            var actual = options.SampleRegions.Select(region => region.PatchIndex).Order().ToArray();
            if (!actual.SequenceEqual(expected))
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Explicit sample regions must be indexed 0 through 19 exactly once.");
            }
        }
    }

    public static IReadOnlyList<Q13SamplePoint> CreateStraightLineSampleCenters(int width, int height, int patchCount = 20)
    {
        var patchWidth = width / (double)patchCount;
        var centerY = height / 2.0;
        return Enumerable.Range(0, patchCount)
            .Select(patchIndex => new Q13SamplePoint(patchIndex, (patchIndex + 0.5) * patchWidth, centerY))
            .ToArray();
    }

    private static IReadOnlyList<Q13SamplePoint> ResolveSampleCenters(int width, int height, Q13MeasurementOptions options)
    {
        if (options.SampleCenters is null)
        {
            return CreateStraightLineSampleCenters(width, height, options.PatchCount);
        }

        return options.SampleCenters
            .OrderBy(point => point.PatchIndex)
            .ToArray();
    }

    private static PatchMeasurement MeasurePatch(Mat image, int patchIndex, int channels, int sampleSize, double centerX, double centerY, bool rejectOutliers, double outlierSigmaThreshold)
    {
        var statistics = PatchSampler.SampleCenteredSquare(image, channels, sampleSize, centerX, centerY, rejectOutliers, outlierSigmaThreshold);
        return CreatePatchMeasurement(patchIndex, statistics);
    }

    private static PatchMeasurement CreatePatchMeasurement(int patchIndex, RgbPatchStatistics statistics)
    {
        return new PatchMeasurement(
            patchIndex,
            statistics.RedMean,
            statistics.GreenMean,
            statistics.BlueMean,
            statistics.RedNoise,
            statistics.GreenNoise,
            statistics.BlueNoise,
            statistics.IsColor,
            statistics.CenterX,
            statistics.CenterY,
            statistics.X,
            statistics.Y,
            statistics.Size);
    }

    private static Q13MeasurementResult MeasureStripGeometry(Mat image, string imagePath, int channels, Q13MeasurementOptions options)
    {
        var geometry = options.StripGeometry!;
        var stripWidth = Math.Max(options.PatchCount * options.SampleSize, (int)Math.Round(geometry.Width));
        var stripHeight = Math.Max(options.SampleSize, (int)Math.Round(geometry.Height));

        using var warped = new Mat();
        var destination = new[]
        {
            new Point2f(0, 0),
            new Point2f(stripWidth - 1, 0),
            new Point2f(stripWidth - 1, stripHeight - 1),
            new Point2f(0, stripHeight - 1)
        };

        using var transform = Cv2.GetPerspectiveTransform(geometry.SourcePoints(), destination);
        Cv2.WarpPerspective(image, warped, transform, new Size(stripWidth, stripHeight), InterpolationFlags.Linear, BorderTypes.Replicate);

        var regions = (options.SampleRegions ?? Q13StripGeometry.CreateDefaultSampleRegions(patchCount: options.PatchCount))
            .OrderBy(region => region.PatchIndex)
            .ToArray();
        var patches = new List<PatchMeasurement>(regions.Length);

        foreach (var region in regions)
        {
            var sampleSize = Math.Max(1, MeasurementMath.MakeOdd((int)Math.Round(region.Size * stripHeight)));
            var centerX = region.CenterX * stripWidth;
            var centerY = region.CenterY * stripHeight;
            var rect = MeasurementGeometry.CenteredSquare(stripWidth, stripHeight, sampleSize, centerX, centerY);
            using var roi = new Mat(warped, rect);
            var originalCenter = geometry.PointAt(region.CenterX, region.CenterY);
            patches.Add(CreatePatchMeasurement(region.PatchIndex, PatchSampler.SampleRgb(roi, channels, options.UseOutlierRejection, options.OutlierSigmaThreshold) with
            {
                CenterX = originalCenter.X,
                CenterY = originalCenter.Y,
                X = rect.X,
                Y = rect.Y,
                Size = rect.Width
            }) with
            {
                ReportSampleCenterX = (int)Math.Round(originalCenter.X),
                ReportSampleCenterY = (int)Math.Round(originalCenter.Y),
                ReportSampleWidth = sampleSize,
                ReportSampleHeight = sampleSize
            });
        }

        return new Q13MeasurementResult(
            imagePath,
            EstimateSamplingPixelsPerInch((int)Math.Round(geometry.Width), options.PatchCount),
            patches[0].SampleSize,
            CalculateInverseGamma(patches, channel: 0),
            CalculateInverseGamma(patches, channel: 1),
            CalculateInverseGamma(patches, channel: 2),
            patches);
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

    private static double EstimateSamplingPixelsPerInch(IReadOnlyList<Q13SamplePoint> sampleCenters, int fallbackImageWidth, int patchCount)
    {
        if (sampleCenters.Count >= 2)
        {
            var ordered = sampleCenters.OrderBy(point => point.PatchIndex).ToArray();
            var first = ordered[0];
            var last = ordered[^1];
            var centerSpan = Math.Sqrt(Math.Pow(last.X - first.X, 2) + Math.Pow(last.Y - first.Y, 2));
            if (centerSpan > 0)
            {
                var inferredStripWidth = centerSpan * patchCount / Math.Max(1, patchCount - 1);
                return EstimateSamplingPixelsPerInch((int)Math.Round(inferredStripWidth), patchCount);
            }
        }

        return EstimateSamplingPixelsPerInch(fallbackImageWidth, patchCount);
    }

}
