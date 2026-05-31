using System.Globalization;
using System.Text;
using Imcheck.Measurement.Measurements.Common;
using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Uniformity;

public sealed class UniformityAnalyzer
{
    private const int MinimumSampleSize = 33;
    private const double DefaultCellCoverage = 0.33;

    private static readonly WhiteSheetSamplePoint[] DefaultSamplePoints =
    [
        new("TopLeft", 0, 0),
        new("TopRight", 2, 0),
        new("Center", 1, 1),
        new("BottomLeft", 0, 2),
        new("BottomRight", 2, 2),
    ];

    public UniformityAnalysisResult Analyze(string imagePath, UniformityAnalysisOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        options ??= new UniformityAnalysisOptions();
        ValidateOptions(options);

        using var image = Cv2.ImRead(imagePath, ImreadModes.Unchanged);
        if (image.Empty())
        {
            throw new InvalidOperationException($"Unable to load image: {imagePath}");
        }

        if (image.Depth() is not (MatType.CV_8U or MatType.CV_16U))
        {
            throw new NotSupportedException("Only 8-bit and 16-bit images are supported for white-sheet analysis.");
        }

        if (image.Channels() is not (1 or 3 or 4))
        {
            throw new NotSupportedException($"Unsupported channel count: {image.Channels()}.");
        }

        var sampleSize = ResolveSampleSize(image.Width, image.Height, options);
        var samples = DefaultSamplePoints
            .Select(point => MeasurePoint(image, point, sampleSize, options))
            .ToArray();

        var maxDeltaL = 0.0;
        var maxDeltaEab = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            for (var j = i + 1; j < samples.Length; j++)
            {
                maxDeltaL = Math.Max(maxDeltaL, Math.Abs(samples[i].LStar - samples[j].LStar));
                maxDeltaEab = Math.Max(maxDeltaEab, DeltaEab(samples[i], samples[j]));
            }
        }

        var illuminationTolerance = UniformityTolerances.IlluminationDeltaL(options.QualityLevel, options.ImagePlaneSize);
        var whiteBalanceTolerance = UniformityTolerances.WhiteBalanceDeltaEab(options.QualityLevel);

        return new UniformityAnalysisResult(
            imagePath,
            image.Width,
            image.Height,
            image.Depth() == MatType.CV_16U ? 16 : 8,
            options.ColorSpace,
            options.QualityLevel,
            options.ImagePlaneSize,
            sampleSize,
            samples,
            maxDeltaL,
            maxDeltaEab,
            illuminationTolerance,
            whiteBalanceTolerance);
    }

    private static WhiteSheetSampleMeasurement MeasurePoint(Mat image, WhiteSheetSamplePoint point, int sampleSize, UniformityAnalysisOptions options)
    {
        var cellWidth = image.Width / 3.0;
        var cellHeight = image.Height / 3.0;
        var centerX = (point.Column + 0.5) * cellWidth - 0.5;
        var centerY = (point.Row + 0.5) * cellHeight - 0.5;
        var rect = MeasurementGeometry.CenteredSquare(image.Width, image.Height, sampleSize, centerX, centerY);
        using var roi = new Mat(image, rect);

        var pixels = rect.Width * rect.Height;
        var redSum = 0.0;
        var greenSum = 0.0;
        var blueSum = 0.0;

        for (var y = 0; y < roi.Height; y++)
        {
            for (var x = 0; x < roi.Width; x++)
            {
                var (red, green, blue) = ReadRgb(roi, x, y);
                redSum += red;
                greenSum += green;
                blueSum += blue;
            }
        }

        var redMean = redSum / pixels;
        var greenMean = greenSum / pixels;
        var blueMean = blueSum / pixels;
        var lab = ColorConversions.ToLab(redMean, greenMean, blueMean, options.ColorSpace, image.Depth() == MatType.CV_16U ? 65535.0 : 255.0);

        return new WhiteSheetSampleMeasurement(
            point.Name,
            centerX,
            centerY,
            rect.X,
            rect.Y,
            rect.Width,
            redMean,
            greenMean,
            blueMean,
            lab.L,
            lab.A,
            lab.B);
    }

    private static (double Red, double Green, double Blue) ReadRgb(Mat image, int x, int y)
    {
        if (image.Depth() == MatType.CV_8U)
        {
            if (image.Channels() == 1)
            {
                var value = image.At<byte>(y, x);
                return (value, value, value);
            }

            var pixel = image.At<Vec3b>(y, x);
            return (pixel.Item2, pixel.Item1, pixel.Item0);
        }

        if (image.Channels() == 1)
        {
            var value = image.At<ushort>(y, x);
            return (value, value, value);
        }

        var widePixel = image.At<Vec3w>(y, x);
        return (widePixel.Item2, widePixel.Item1, widePixel.Item0);
    }

    private static double DeltaEab(WhiteSheetSampleMeasurement first, WhiteSheetSampleMeasurement second)
    {
        var deltaA = first.AStar - second.AStar;
        var deltaB = first.BStar - second.BStar;
        return Math.Sqrt(deltaA * deltaA + deltaB * deltaB);
    }

    private static void ValidateOptions(UniformityAnalysisOptions options)
    {
        if (options.SampleSize is null)
        {
            return;
        }

        if (options.SampleSize < MinimumSampleSize || options.SampleSize % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SampleSize), $"Sample size must be an odd number of at least {MinimumSampleSize} pixels.");
        }
    }

    private static int ResolveSampleSize(int imageWidth, int imageHeight, UniformityAnalysisOptions options)
    {
        var cellWidth = imageWidth / 3.0;
        var cellHeight = imageHeight / 3.0;
        var sampleSize = options.SampleSize ?? AutoSampleSize(cellWidth, cellHeight);

        if (sampleSize > cellWidth || sampleSize > cellHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.SampleSize),
                $"Sample size {sampleSize} does not fit inside each 3x3 grid cell ({cellWidth:0.##}x{cellHeight:0.##} pixels).");
        }

        return sampleSize;
    }

    private static int AutoSampleSize(double cellWidth, double cellHeight)
    {
        var sampleSize = (int)Math.Round(Math.Min(cellWidth, cellHeight) * DefaultCellCoverage);
        if (sampleSize % 2 == 0)
        {
            sampleSize = MeasurementMath.MakeOdd(sampleSize);
        }

        return Math.Max(MinimumSampleSize, sampleSize);
    }

    private sealed record WhiteSheetSamplePoint(string Name, int Column, int Row);
}

public sealed record UniformityAnalysisOptions
{
    public int? SampleSize { get; init; }

    public RgbColorSpace ColorSpace { get; init; } = RgbColorSpace.SRgb;

    public UniformityQualityLevel QualityLevel { get; init; } = UniformityQualityLevel.Full;

    public UniformityImagePlaneSize ImagePlaneSize { get; init; } = UniformityImagePlaneSize.UpToA3;
}

public sealed record UniformityAnalysisResult(
    string ImagePath,
    int ImageWidth,
    int ImageHeight,
    int BitDepth,
    RgbColorSpace ColorSpace,
    UniformityQualityLevel QualityLevel,
    UniformityImagePlaneSize ImagePlaneSize,
    int SampleSize,
    IReadOnlyList<WhiteSheetSampleMeasurement> Samples,
    double MaxDeltaLStar,
    double MaxDeltaEab,
    double? IlluminationDeltaLStarTolerance,
    double WhiteBalanceDeltaEabTolerance)
{
    public bool? IlluminationPass => IlluminationDeltaLStarTolerance is null
        ? null
        : MaxDeltaLStar <= IlluminationDeltaLStarTolerance;

    public bool WhiteBalancePass => MaxDeltaEab <= WhiteBalanceDeltaEabTolerance;

    public string ToCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Section,Metric,Value,Tolerance,Pass");
        AppendSummary(builder, "Illumination", "MaxDeltaLStar", MaxDeltaLStar, IlluminationDeltaLStarTolerance, IlluminationPass);
        AppendSummary(builder, "WhiteBalance", "MaxDeltaEab", MaxDeltaEab, WhiteBalanceDeltaEabTolerance, WhiteBalancePass);
        builder.AppendLine();
        builder.AppendLine("Name,MeanRed,MeanGreen,MeanBlue,LStar,AStar,BStar,SampleTopLeftX,SampleTopLeftY,SampleTopRightX,SampleTopRightY,SampleBottomRightX,SampleBottomRightY,SampleBottomLeftX,SampleBottomLeftY");
        foreach (var sample in Samples)
        {
            builder.Append(sample.Name).Append(',')
                .Append(Format(sample.MeanRed)).Append(',')
                .Append(Format(sample.MeanGreen)).Append(',')
                .Append(Format(sample.MeanBlue)).Append(',')
                .Append(Format(sample.LStar)).Append(',')
                .Append(Format(sample.AStar)).Append(',')
                .Append(Format(sample.BStar)).Append(',')
                .Append(sample.SampleTopLeftX).Append(',')
                .Append(sample.SampleTopLeftY).Append(',')
                .Append(sample.SampleTopRightX).Append(',')
                .Append(sample.SampleTopRightY).Append(',')
                .Append(sample.SampleBottomRightX).Append(',')
                .Append(sample.SampleBottomRightY).Append(',')
                .Append(sample.SampleBottomLeftX).Append(',')
                .Append(sample.SampleBottomLeftY).AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendSummary(StringBuilder builder, string section, string metric, double value, double? tolerance, bool? pass)
    {
        builder.Append(section).Append(',')
            .Append(metric).Append(',')
            .Append(Format(value)).Append(',')
            .Append(tolerance is null ? "Not specified" : Format(tolerance.Value)).Append(',')
            .Append(pass is null ? "Not specified" : pass.Value ? "Pass" : "Fail").AppendLine();
    }

    private static string Format(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}

public sealed record WhiteSheetSampleMeasurement(
    string Name,
    double SampleCenterX,
    double SampleCenterY,
    int SampleX,
    int SampleY,
    int SampleSize,
    double MeanRed,
    double MeanGreen,
    double MeanBlue,
    double LStar,
    double AStar,
    double BStar)
{
    public int SampleTopLeftX => SampleX;

    public int SampleTopLeftY => SampleY;

    public int SampleTopRightX => SampleX + SampleSize;

    public int SampleTopRightY => SampleY;

    public int SampleBottomRightX => SampleX + SampleSize;

    public int SampleBottomRightY => SampleY + SampleSize;

    public int SampleBottomLeftX => SampleX;

    public int SampleBottomLeftY => SampleY + SampleSize;
}

public enum RgbColorSpace
{
    SRgb,
    AdobeRgb1998,
    EciRgbV2
}

public enum UniformityQualityLevel
{
    Full,
    Light,
    ExtraLight
}

public enum UniformityImagePlaneSize
{
    UpToA3,
    UpToA2,
    UpToA1,
    UpToA0
}
