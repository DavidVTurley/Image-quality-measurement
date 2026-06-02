using OpenCvSharp;

namespace Imcheck.Measurement;

public sealed class Qa62TargetGenerator : ITargetGenerator<Qa62TargetGeneratorOptions, Qa62TargetGeneratorResult>
{
    public const int DefaultDpi = 600;
    public const double TargetWidthMillimeters = 76.2;
    public const double TargetHeightMillimeters = 95.25;

    private static readonly (byte Red, byte Green, byte Blue)[] PatchColors =
    [
        (222, 224, 223),
        (205, 205, 203),
        (197, 197, 195),
        (181, 181, 177),
        (163, 163, 158),
        (148, 147, 143),
        (138, 137, 133),
        (132, 131, 128),
        (124, 123, 119),
        (117, 116, 113),
        (105, 105, 101),
        (96, 95, 92),
        (90, 90, 87),
        (86, 86, 83),
        (81, 81, 79),
        (79, 79, 77),
        (73, 73, 71),
        (66, 66, 65),
        (56, 56, 56),
        (54, 54, 54),
    ];

    private static readonly (double X, double Y)[] PatchCenters =
    [
        (169, 196),
        (282, 196),
        (395, 196),
        (508, 196),
        (621, 196),
        (734, 196),
        (734, 309),
        (734, 422),
        (734, 535),
        (734, 648),
        (734, 725),
        (621, 725),
        (508, 725),
        (395, 725),
        (282, 725),
        (169, 725),
        (169, 648),
        (169, 535),
        (169, 422),
        (169, 309),
    ];

    private static readonly (double X, double Y)[] RegistrationCrossCenters =
    [
        (51, 91),
        (841, 87),
        (51, 874),
        (841, 874),
    ];

    public Qa62TargetGeneratorResult Generate(string outputPath, Qa62TargetGeneratorOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        options ??= new Qa62TargetGeneratorOptions();
        ValidateOptions(options);

        var width = TargetRendering.PixelsForMillimeters(TargetWidthMillimeters, options.Dpi);
        var height = TargetRendering.PixelsForMillimeters(TargetHeightMillimeters, options.Dpi);
        var scaleX = width / 913.0;
        var scaleY = height / 1176.0;

        using var image = new Mat(height, width, MatType.CV_8UC3, Color(244, 244, 242));

        DrawRegistrationCrosses(image, scaleX, scaleY);
        DrawMainField(image, scaleX, scaleY);
        DrawSlantedSquare(image, scaleX, scaleY);
        DrawPatches(image, scaleX, scaleY);
        DrawBottomIdentification(image, scaleX, scaleY);

        TargetRendering.EnsureOutputDirectory(outputPath);

        if (!Cv2.ImWrite(outputPath, image))
        {
            throw new InvalidOperationException($"Unable to write generated QA-62 target: {outputPath}");
        }

        return new Qa62TargetGeneratorResult(outputPath, width, height, options.Dpi);
    }

    private static void DrawRegistrationCrosses(Mat image, double scaleX, double scaleY)
    {
        foreach (var (x, y) in RegistrationCrossCenters)
        {
            var center = Point(x, y, scaleX, scaleY);
            var halfLengthX = ScaleX(27, scaleX);
            var halfLengthY = ScaleY(27, scaleY);
            var thickness = Math.Max(4, ScaleX(4, scaleX));
            Cv2.Line(image, new Point(center.X - halfLengthX, center.Y), new Point(center.X + halfLengthX, center.Y), Color(74, 74, 74), thickness, LineTypes.AntiAlias);
            Cv2.Line(image, new Point(center.X, center.Y - halfLengthY), new Point(center.X, center.Y + halfLengthY), Color(74, 74, 74), thickness, LineTypes.AntiAlias);
        }
    }

    private static void DrawMainField(Mat image, double scaleX, double scaleY)
    {
        var rect = RectFromReference(225, 252, 447, 455, scaleX, scaleY);
        Cv2.Rectangle(image, rect, Color(156, 154, 148), -1, LineTypes.AntiAlias);
    }

    private static void DrawSlantedSquare(Mat image, double scaleX, double scaleY)
    {
        var center = Point(447, 458, scaleX, scaleY);
        var size = new Size2f(322 * (float)scaleX, 322 * (float)scaleY);
        var rotated = new RotatedRect(new Point2f(center.X, center.Y), size, 5f);
        var points = rotated.Points().Select(point => new Point((int)Math.Round(point.X), (int)Math.Round(point.Y))).ToArray();
        Cv2.FillConvexPoly(image, points, Color(75, 75, 72), LineTypes.AntiAlias);
    }

    private static void DrawPatches(Mat image, double scaleX, double scaleY)
    {
        var patchWidth = 114.0;
        var patchHeight = 112.0;
        var chamfer = 2.0;
        var border = Color(41, 41, 41);
        var borderThickness = Math.Max(2, ScaleX(2, scaleX));

        for (var i = 0; i < PatchCenters.Length; i++)
        {
            var center = PatchCenters[i];
            var left = center.X - patchWidth / 2.0;
            var top = center.Y - patchHeight / 2.0;
            var right = center.X + patchWidth / 2.0;
            var bottom = center.Y + patchHeight / 2.0;
            var polygon = new[]
            {
                Point(left, top, scaleX, scaleY),
                Point(right, top, scaleX, scaleY),
                Point(right, bottom - chamfer, scaleX, scaleY),
                Point(right - chamfer, bottom, scaleX, scaleY),
                Point(left + chamfer, bottom, scaleX, scaleY),
                Point(left, bottom - chamfer, scaleX, scaleY),
            };

            Cv2.FillConvexPoly(image, polygon, Color(PatchColors[i]), LineTypes.AntiAlias);
            Cv2.Polylines(image, [polygon], true, border, borderThickness, LineTypes.AntiAlias);
        }
    }

    private static void DrawBottomIdentification(Mat image, double scaleX, double scaleY)
    {
        var textColor = Color(85, 85, 85);
        PutCenteredText(image, "SCANNER SFR & OEC #2", 456, 887, 2.0, 3, textColor, scaleX, scaleY);
        PutCenteredText(image, "APPLIED", 291, 963, 0.95, 2, textColor, scaleX, scaleY);
        PutCenteredText(image, "IMAGE", 291, 1014, 1.05, 2, textColor, scaleX, scaleY);
        PutCenteredText(image, "Inc", 291, 1067, 1.1, 2, textColor, scaleX, scaleY);
        PutText(image, "1653 East Main Street", 475, 954, 0.55, 1, textColor, scaleX, scaleY);
        PutText(image, "Rochester, NY 14609 USA", 475, 987, 0.55, 1, textColor, scaleX, scaleY);
        PutText(image, "Voice: (585) 482-0300", 475, 1020, 0.55, 1, textColor, scaleX, scaleY);
        PutText(image, "www.appliedimage.com", 475, 1062, 0.55, 1, textColor, scaleX, scaleY);
        PutCenteredText(image, "(c) 2003, 2005, APPLIED IMAGE, Inc., All Rights Reserved Rev. 1.02", 456, 1114, 0.5, 1, textColor, scaleX, scaleY);
        PutCenteredText(image, "PN: QA-62-SFR-P-RP", 456, 1152, 1.35, 2, textColor, scaleX, scaleY);

        DrawAppliedImageMark(image, scaleX, scaleY);
    }

    private static void DrawAppliedImageMark(Mat image, double scaleX, double scaleY)
    {
        var mark = new[]
        {
            Point(364, 1065, scaleX, scaleY),
            Point(426, 950, scaleX, scaleY),
            Point(481, 1065, scaleX, scaleY),
        };
        Cv2.FillConvexPoly(image, mark, Color(85, 85, 85), LineTypes.AntiAlias);

        for (var i = 0; i < 6; i++)
        {
            var y = 1048 - i * 16;
            var x0 = 378 + i * 7;
            var x1 = 467 - i * 8;
            Cv2.Line(image, Point(x0, y, scaleX, scaleY), Point(x1, y, scaleX, scaleY), Color(244, 244, 242), Math.Max(3, ScaleY(4, scaleY)), LineTypes.AntiAlias);
        }
    }

    private static void PutCenteredText(
        Mat image,
        string text,
        double centerX,
        double baselineY,
        double fontScale,
        int thickness,
        Scalar color,
        double scaleX,
        double scaleY)
    {
        TargetRendering.PutScaledCenteredText(image, text, centerX, baselineY, fontScale, thickness, color, scaleX, scaleY);
    }

    private static void PutText(
        Mat image,
        string text,
        double x,
        double y,
        double fontScale,
        int thickness,
        Scalar color,
        double scaleX,
        double scaleY)
    {
        TargetRendering.PutScaledText(image, text, x, y, fontScale, thickness, color, scaleX, scaleY);
    }

    private static Rect RectFromReference(double x, double y, double width, double height, double scaleX, double scaleY)
    {
        return TargetRendering.Rect(x, y, width, height, scaleX, scaleY);
    }

    private static Point Point(double x, double y, double scaleX, double scaleY)
    {
        return TargetRendering.Point(x, y, scaleX, scaleY);
    }

    private static Scalar Color((byte Red, byte Green, byte Blue) color)
    {
        return TargetRendering.Color(color);
    }

    private static Scalar Color(byte red, byte green, byte blue)
    {
        return TargetRendering.Color(red, green, blue);
    }

    private static int PixelsForMillimeters(double millimeters, int dpi)
    {
        return TargetRendering.PixelsForMillimeters(millimeters, dpi);
    }

    private static int ScaleX(double value, double scaleX)
    {
        return TargetRendering.ScaleX(value, scaleX);
    }

    private static int ScaleY(double value, double scaleY)
    {
        return TargetRendering.ScaleY(value, scaleY);
    }

    private static void ValidateOptions(Qa62TargetGeneratorOptions options)
    {
        if (options.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Dpi), "DPI must be positive.");
        }
    }
}

public sealed record Qa62TargetGeneratorOptions
{
    public int Dpi { get; init; } = Qa62TargetGenerator.DefaultDpi;
}

public sealed record Qa62TargetGeneratorResult(string OutputPath, int Width, int Height, int Dpi);
