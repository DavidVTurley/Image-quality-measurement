using System.Numerics;
using Imcheck.Measurement.Measurements;
using Imcheck.Measurement.Measurements.Common;
using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Qa62;

public sealed class Qa62Measurer : IImageMeasurer<Qa62MeasurementOptions, Qa62MeasurementResult>
{
    private const int PatchCount = 20;
    private const int Oversampling = 4;

    private static readonly (double X, double Y)[] PatchCenters =
    [
        (0.1851, 0.1667),
        (0.3089, 0.1667),
        (0.4326, 0.1667),
        (0.5564, 0.1667),
        (0.6802, 0.1667),
        (0.8040, 0.1667),
        (0.8040, 0.2628),
        (0.8040, 0.3588),
        (0.8040, 0.4549),
        (0.8040, 0.5510),
        (0.8040, 0.6165),
        (0.6802, 0.6165),
        (0.5564, 0.6165),
        (0.4326, 0.6165),
        (0.3089, 0.6165),
        (0.1851, 0.6165),
        (0.1851, 0.5510),
        (0.1851, 0.4549),
        (0.1851, 0.3588),
        (0.1851, 0.2628),
    ];

    public Qa62MeasurementResult Measure(string imagePath, Qa62MeasurementOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        options ??= new Qa62MeasurementOptions();

        using var image = ImageValidation.LoadRequired(imagePath, ImreadModes.Unchanged, "QA-62 measurement");
        var channels = ImageValidation.RequireEightBitChannels(image, "QA-62");

        var bounds = options.TargetBounds ?? new Qa62TargetBounds(0, 0, image.Width, image.Height);
        var sampleSize = options.SampleSize ?? AutoPatchSampleSize(bounds);
        ValidateOptions(sampleSize, options.SamplingPixelsPerInch);

        var patches = MeasurePatches(image, channels, bounds, sampleSize);
        var sfr = MeasureSfr(image, channels, bounds, options.SamplingPixelsPerInch);

        return new Qa62MeasurementResult(
            imagePath,
            options.SamplingPixelsPerInch,
            patches,
            sfr.Summary,
            sfr.Curve);
    }

    private static IReadOnlyList<Qa62PatchMeasurement> MeasurePatches(Mat image, int channels, Qa62TargetBounds bounds, int sampleSize)
    {
        var patches = new List<Qa62PatchMeasurement>(PatchCount);
        for (var i = 0; i < PatchCenters.Length; i++)
        {
            var centerX = bounds.X + PatchCenters[i].X * bounds.Width;
            var centerY = bounds.Y + PatchCenters[i].Y * bounds.Height;
            var statistics = PatchSampler.SampleCenteredSquare(image, channels, sampleSize, centerX, centerY);

            patches.Add(new Qa62PatchMeasurement(
                i + 1,
                statistics.RedMean,
                statistics.GreenMean,
                statistics.BlueMean,
                statistics.RedNoise,
                statistics.GreenNoise,
                statistics.BlueNoise,
                centerX,
                centerY,
                statistics.X,
                statistics.Y,
                statistics.Size));
        }

        return patches;
    }

    private static (Qa62SfrSummary Summary, IReadOnlyList<Qa62SfrCurvePoint> Curve) MeasureSfr(
        Mat image,
        int channels,
        Qa62TargetBounds bounds,
        double samplingPixelsPerInch)
    {
        var pixelsPerMillimeter = samplingPixelsPerInch / 25.4;
        var horizontal = AverageProfiles(
            MeasureEdge(image, channels, bounds, new NormalizedRect(0.2930, 0.2721, 0.0931, 0.2636), EdgeOrientation.Vertical),
            MeasureEdge(image, channels, bounds, new NormalizedRect(0.5887, 0.2721, 0.0931, 0.2636), EdgeOrientation.Vertical));
        var vertical = AverageProfiles(
            MeasureEdge(image, channels, bounds, new NormalizedRect(0.4599, 0.2657, 0.1752, 0.0468), EdgeOrientation.Horizontal),
            MeasureEdge(image, channels, bounds, new NormalizedRect(0.4599, 0.4996, 0.1752, 0.0468), EdgeOrientation.Horizontal));

        var curve = BuildCurve(horizontal, vertical, pixelsPerMillimeter);
        var summary = new Qa62SfrSummary(
            SamplingEfficiency(horizontal, pixelsPerMillimeter),
            SamplingEfficiency(vertical, pixelsPerMillimeter),
            FrequencyAt(horizontal, pixelsPerMillimeter, 0.1),
            FrequencyAt(vertical, pixelsPerMillimeter, 0.1),
            FrequencyAt(horizontal, pixelsPerMillimeter, 0.5),
            FrequencyAt(vertical, pixelsPerMillimeter, 0.5),
            horizontal.Misregistration,
            vertical.Misregistration);

        return (summary, curve);
    }

    private static EdgeProfile MeasureEdge(Mat image, int channels, Qa62TargetBounds bounds, NormalizedRect normalizedRect, EdgeOrientation orientation)
    {
        var rect = ToRect(image.Width, image.Height, bounds, normalizedRect);
        using var roi = new Mat(image, rect);
        var luminance = ReadChannel(roi, channels, Channel.Luminance);
        var red = ReadChannel(roi, channels, Channel.Red);
        var green = ReadChannel(roi, channels, Channel.Green);
        var blue = ReadChannel(roi, channels, Channel.Blue);

        var line = FitEdgeLine(luminance, orientation);
        var redOffset = EdgeOffset(red, line, orientation);
        var greenOffset = EdgeOffset(green, line, orientation);
        var blueOffset = EdgeOffset(blue, line, orientation);
        var averageOffset = (redOffset + greenOffset + blueOffset) / 3.0;
        var redMisregistration = redOffset - averageOffset;
        var greenMisregistration = greenOffset - averageOffset;
        var blueMisregistration = blueOffset - averageOffset;

        return new EdgeProfile(
            BuildSfr(red, line, orientation),
            BuildSfr(green, line, orientation),
            BuildSfr(blue, line, orientation),
            BuildSfr(luminance, line, orientation),
            new Qa62ChannelValues(
                redMisregistration,
                greenMisregistration,
                blueMisregistration,
                Math.Max(Math.Abs(redMisregistration), Math.Max(Math.Abs(greenMisregistration), Math.Abs(blueMisregistration)))));
    }

    private static EdgeProfile AverageProfiles(EdgeProfile first, EdgeProfile second)
    {
        return new EdgeProfile(
            Average(first.Red, second.Red),
            Average(first.Green, second.Green),
            Average(first.Blue, second.Blue),
            Average(first.Luminance, second.Luminance),
            new Qa62ChannelValues(
                (first.Misregistration.Red + second.Misregistration.Red) / 2.0,
                (first.Misregistration.Green + second.Misregistration.Green) / 2.0,
                (first.Misregistration.Blue + second.Misregistration.Blue) / 2.0,
                (first.Misregistration.Luminance + second.Misregistration.Luminance) / 2.0));
    }

    private static IReadOnlyList<Qa62SfrCurvePoint> BuildCurve(EdgeProfile horizontal, EdgeProfile vertical, double pixelsPerMillimeter)
    {
        const int imcheckCurveRows = 60;
        var points = new List<Qa62SfrCurvePoint>(imcheckCurveRows);
        var finalFrequency = pixelsPerMillimeter * 0.75;
        var frequencyStep = finalFrequency / (imcheckCurveRows - 1);
        for (var i = 0; i < imcheckCurveRows; i++)
        {
            var frequency = i * frequencyStep;
            points.Add(new Qa62SfrCurvePoint(
                frequency,
                InterpolateSfr(horizontal.Red, pixelsPerMillimeter, frequency),
                InterpolateSfr(horizontal.Green, pixelsPerMillimeter, frequency),
                InterpolateSfr(horizontal.Blue, pixelsPerMillimeter, frequency),
                InterpolateSfr(horizontal.Luminance, pixelsPerMillimeter, frequency),
                InterpolateSfr(vertical.Red, pixelsPerMillimeter, frequency),
                InterpolateSfr(vertical.Green, pixelsPerMillimeter, frequency),
                InterpolateSfr(vertical.Blue, pixelsPerMillimeter, frequency),
                InterpolateSfr(vertical.Luminance, pixelsPerMillimeter, frequency)));
        }

        return points;
    }

    private static Qa62ChannelValues FrequencyAt(EdgeProfile profile, double pixelsPerMillimeter, double threshold)
    {
        return new Qa62ChannelValues(
            FrequencyAt(profile.Red, profile.Red.Count, pixelsPerMillimeter, threshold),
            FrequencyAt(profile.Green, profile.Green.Count, pixelsPerMillimeter, threshold),
            FrequencyAt(profile.Blue, profile.Blue.Count, pixelsPerMillimeter, threshold),
            FrequencyAt(profile.Luminance, profile.Luminance.Count, pixelsPerMillimeter, threshold));
    }

    private static Qa62ChannelValues SamplingEfficiency(EdgeProfile profile, double pixelsPerMillimeter)
    {
        var nyquist = pixelsPerMillimeter / 2.0;
        var sfr10 = FrequencyAt(profile, pixelsPerMillimeter, 0.1);
        return new Qa62ChannelValues(
            100.0 * sfr10.Red / nyquist,
            100.0 * sfr10.Green / nyquist,
            100.0 * sfr10.Blue / nyquist,
            100.0 * sfr10.Luminance / nyquist);
    }

    private static double FrequencyAt(IReadOnlyList<double> sfr, int length, double pixelsPerMillimeter, double threshold)
    {
        for (var i = 1; i < length; i++)
        {
            var x1 = FrequencyForIndex(i, length, pixelsPerMillimeter);
            if (x1 > pixelsPerMillimeter / 2.0)
            {
                return pixelsPerMillimeter / 2.0;
            }

            if (sfr[i] > threshold || sfr[i - 1] < threshold)
            {
                continue;
            }

            var x0 = FrequencyForIndex(i - 1, length, pixelsPerMillimeter);
            var y0 = sfr[i - 1];
            var y1 = sfr[i];
            if (Math.Abs(y1 - y0) < double.Epsilon)
            {
                return x1;
            }

            return x0 + (threshold - y0) * (x1 - x0) / (y1 - y0);
        }

        return pixelsPerMillimeter / 2.0;
    }

    private static double InterpolateSfr(IReadOnlyList<double> sfr, double pixelsPerMillimeter, double frequency)
    {
        if (sfr.Count == 0)
        {
            return double.NaN;
        }

        var step = FrequencyForIndex(1, sfr.Count, pixelsPerMillimeter);
        if (step <= 0 || frequency <= 0)
        {
            return sfr[0];
        }

        var position = frequency / step;
        var left = (int)Math.Floor(position);
        if (left >= sfr.Count - 1)
        {
            return sfr[sfr.Count - 1];
        }

        var fraction = position - left;
        return sfr[left] + (sfr[left + 1] - sfr[left]) * fraction;
    }

    private static double FrequencyForIndex(int index, int length, double pixelsPerMillimeter)
    {
        return index * Oversampling * pixelsPerMillimeter / length;
    }

    private static double[] BuildSfr(double[,] channel, EdgeLine line, EdgeOrientation orientation)
    {
        var samples = ProjectSamples(channel, line, orientation);
        if (samples.Count < 32)
        {
            return [1.0];
        }

        var minBin = samples.Min(sample => sample.Bin);
        var maxBin = samples.Max(sample => sample.Bin);
        var binCount = maxBin - minBin + 1;
        var sums = new double[binCount];
        var counts = new int[binCount];

        foreach (var sample in samples)
        {
            var index = sample.Bin - minBin;
            sums[index] += sample.Value;
            counts[index]++;
        }

        var esf = new List<double>(binCount);
        for (var i = 0; i < binCount; i++)
        {
            if (counts[i] > 0)
            {
                esf.Add(sums[i] / counts[i]);
            }
        }

        if (esf.Count < 16)
        {
            return [1.0];
        }

        var lsf = new double[esf.Count - 1];
        for (var i = 0; i < lsf.Length; i++)
        {
            lsf[i] = esf[i + 1] - esf[i];
        }

        for (var i = 0; i < lsf.Length; i++)
        {
            lsf[i] *= 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * i / Math.Max(1, lsf.Length - 1));
        }

        var half = lsf.Length / 2;
        var sfr = new double[half];
        for (var k = 0; k < half; k++)
        {
            var sum = Complex.Zero;
            for (var n = 0; n < lsf.Length; n++)
            {
                var angle = -2.0 * Math.PI * k * n / lsf.Length;
                sum += lsf[n] * Complex.FromPolarCoordinates(1.0, angle);
            }

            sfr[k] = sum.Magnitude;
        }

        var normalizer = sfr[0] == 0 ? 1.0 : sfr[0];
        for (var i = 0; i < sfr.Length; i++)
        {
            sfr[i] /= normalizer;
        }

        return sfr;
    }

    private static double EdgeOffset(double[,] channel, EdgeLine line, EdgeOrientation orientation)
    {
        var points = LocateEdgePoints(channel, orientation);
        if (points.Count == 0)
        {
            return 0;
        }

        return orientation == EdgeOrientation.Vertical
            ? points.Average(point => point.X - (line.Slope * point.Y + line.Intercept))
            : points.Average(point => point.Y - (line.Slope * point.X + line.Intercept));
    }

    private static List<(int Bin, double Value)> ProjectSamples(double[,] channel, EdgeLine line, EdgeOrientation orientation)
    {
        var height = channel.GetLength(0);
        var width = channel.GetLength(1);
        var samples = new List<(int Bin, double Value)>(width * height);
        var denominator = Math.Sqrt(1.0 + line.Slope * line.Slope);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distance = orientation == EdgeOrientation.Vertical
                    ? (x - (line.Slope * y + line.Intercept)) / denominator
                    : (y - (line.Slope * x + line.Intercept)) / denominator;
                var bin = (int)Math.Round(distance * Oversampling);
                samples.Add((bin, channel[y, x]));
            }
        }

        return samples;
    }

    private static EdgeLine FitEdgeLine(double[,] luminance, EdgeOrientation orientation)
    {
        var points = LocateEdgePoints(luminance, orientation);
        if (points.Count < 2)
        {
            return new EdgeLine(0, orientation == EdgeOrientation.Vertical ? luminance.GetLength(1) / 2.0 : luminance.GetLength(0) / 2.0);
        }

        if (orientation == EdgeOrientation.Vertical)
        {
            var averageY = points.Average(point => point.Y);
            var averageX = points.Average(point => point.X);
            var denominator = points.Sum(point => Math.Pow(point.Y - averageY, 2));
            var slope = denominator == 0 ? 0 : points.Sum(point => (point.Y - averageY) * (point.X - averageX)) / denominator;
            return new EdgeLine(slope, averageX - slope * averageY);
        }

        var avgX = points.Average(point => point.X);
        var avgY = points.Average(point => point.Y);
        var denom = points.Sum(point => Math.Pow(point.X - avgX, 2));
        var horizontalSlope = denom == 0 ? 0 : points.Sum(point => (point.X - avgX) * (point.Y - avgY)) / denom;
        return new EdgeLine(horizontalSlope, avgY - horizontalSlope * avgX);
    }

    private static List<EdgePoint> LocateEdgePoints(double[,] channel, EdgeOrientation orientation)
    {
        var height = channel.GetLength(0);
        var width = channel.GetLength(1);
        var points = new List<EdgePoint>();

        if (orientation == EdgeOrientation.Vertical)
        {
            for (var y = 2; y < height - 2; y++)
            {
                var bestX = 0;
                var bestGradient = 0.0;
                for (var x = 2; x < width - 2; x++)
                {
                    var gradient = Math.Abs(channel[y, x + 1] - channel[y, x - 1]);
                    if (gradient > bestGradient)
                    {
                        bestGradient = gradient;
                        bestX = x;
                    }
                }

                if (bestGradient > 5.0)
                {
                    points.Add(new EdgePoint(bestX, y));
                }
            }

            return points;
        }

        for (var x = 2; x < width - 2; x++)
        {
            var bestY = 0;
            var bestGradient = 0.0;
            for (var y = 2; y < height - 2; y++)
            {
                var gradient = Math.Abs(channel[y + 1, x] - channel[y - 1, x]);
                if (gradient > bestGradient)
                {
                    bestGradient = gradient;
                    bestY = y;
                }
            }

            if (bestGradient > 5.0)
            {
                points.Add(new EdgePoint(x, bestY));
            }
        }

        return points;
    }

    private static double[,] ReadChannel(Mat mat, int channels, Channel channel)
    {
        var values = new double[mat.Height, mat.Width];
        for (var y = 0; y < mat.Height; y++)
        {
            for (var x = 0; x < mat.Width; x++)
            {
                if (channels == 1)
                {
                    values[y, x] = mat.At<byte>(y, x);
                    continue;
                }

                var pixel = mat.At<Vec3b>(y, x);
                var blue = pixel.Item0;
                var green = pixel.Item1;
                var red = pixel.Item2;
                values[y, x] = channel switch
                {
                    Channel.Red => red,
                    Channel.Green => green,
                    Channel.Blue => blue,
                    _ => 0.2126 * red + 0.7152 * green + 0.0722 * blue
                };
            }
        }

        return values;
    }

    private static double[] Average(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        var length = Math.Min(first.Count, second.Count);
        var average = new double[length];
        for (var i = 0; i < length; i++)
        {
            average[i] = (first[i] + second[i]) / 2.0;
        }

        return average;
    }

    private static Rect ToRect(int imageWidth, int imageHeight, Qa62TargetBounds bounds, NormalizedRect normalizedRect)
    {
        var x = MeasurementMath.Clamp((int)Math.Round(bounds.X + normalizedRect.X * bounds.Width), 0, imageWidth - 1);
        var y = MeasurementMath.Clamp((int)Math.Round(bounds.Y + normalizedRect.Y * bounds.Height), 0, imageHeight - 1);
        var width = MeasurementMath.Clamp((int)Math.Round(normalizedRect.Width * bounds.Width), 1, imageWidth - x);
        var height = MeasurementMath.Clamp((int)Math.Round(normalizedRect.Height * bounds.Height), 1, imageHeight - y);
        return new Rect(x, y, width, height);
    }

    private static int AutoPatchSampleSize(Qa62TargetBounds bounds)
    {
        var size = (int)Math.Round(Math.Min(bounds.Width, bounds.Height) * 0.0265);
        if (size % 2 == 0)
        {
            size++;
        }

        return Math.Max(9, size);
    }

    private static void ValidateOptions(int sampleSize, double samplingPixelsPerInch)
    {
        if (sampleSize <= 0 || sampleSize % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be a positive odd number.");
        }

        if (samplingPixelsPerInch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(samplingPixelsPerInch), "Sampling must be positive.");
        }
    }

    private sealed record EdgeProfile(
        IReadOnlyList<double> Red,
        IReadOnlyList<double> Green,
        IReadOnlyList<double> Blue,
        IReadOnlyList<double> Luminance,
        Qa62ChannelValues Misregistration);

    private sealed record EdgeLine(double Slope, double Intercept);

    private sealed record EdgePoint(double X, double Y);

    private sealed record NormalizedRect(double X, double Y, double Width, double Height);

    private enum EdgeOrientation
    {
        Vertical,
        Horizontal
    }

    private enum Channel
    {
        Red,
        Green,
        Blue,
        Luminance
    }
}
