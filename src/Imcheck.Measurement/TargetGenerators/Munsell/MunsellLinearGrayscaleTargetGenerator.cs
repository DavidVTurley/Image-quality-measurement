using OpenCvSharp;

namespace Imcheck.Measurement;

public sealed class MunsellLinearGrayscaleTargetGenerator
{
    public const int DefaultDpi = 600;
    public const double TargetWidthMillimeters = 255.0;
    public const double TargetHeightMillimeters = 32.0;

    public static IReadOnlyList<MunsellLinearGrayscalePatch> Patches { get; } =
    [
        new("G1", 3.5),
        new("95", 95.0),
        new("90", 90.0),
        new("85", 85.0),
        new("80", 80.0),
        new("75", 75.0),
        new("70", 70.0),
        new("65", 65.0),
        new("60", 60.0),
        new("55", 55.0),
        new("50", 50.0),
        new("45", 45.0),
        new("40", 40.0),
        new("35", 35.0),
        new("30", 30.0),
        new("25", 25.0),
        new("20", 20.0),
        new("15", 15.0),
        new("10", 10.0),
        new("5", 5.0),
        new("G2", 3.5),
    ];

    public MunsellLinearGrayscaleTargetGeneratorResult Generate(string outputPath, MunsellLinearGrayscaleTargetGeneratorOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        options ??= new MunsellLinearGrayscaleTargetGeneratorOptions();
        ValidateOptions(options);

        var width = PixelsForMillimeters(TargetWidthMillimeters, options.Dpi);
        var height = PixelsForMillimeters(TargetHeightMillimeters, options.Dpi);

        using var image = Render(width, height, options);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!Cv2.ImWrite(outputPath, image))
        {
            throw new InvalidOperationException($"Unable to write generated Munsell Linear Grayscale target: {outputPath}");
        }

        return new MunsellLinearGrayscaleTargetGeneratorResult(outputPath, width, height, options.Dpi);
    }

    public Mat Render(int width, int height, MunsellLinearGrayscaleTargetGeneratorOptions? options = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        options ??= new MunsellLinearGrayscaleTargetGeneratorOptions();
        ValidateOptions(options);

        var scaleX = width / TargetWidthMillimeters;
        var scaleY = height / TargetHeightMillimeters;
        var image = new Mat(height, width, MatType.CV_8UC3, Color(246, 245, 241));

        DrawOuterBorder(image, scaleX, scaleY);
        if (options.ShowTitle)
        {
            DrawTitle(image, scaleX, scaleY);
        }

        var patchRects = DrawPatchRow(image, scaleX, scaleY);
        GrayscaleNoiseApplicator.Apply(image, patchRects, options.Noise);

        if (options.ShowLabels)
        {
            DrawPatchLabels(image, scaleX, scaleY);
        }

        if (options.ShowMillimeterScale)
        {
            DrawMillimeterScale(image, scaleX, scaleY);
        }

        return image;
    }

    public static MunsellLinearGrayscalePatchLayout GetPatchLayout(int patchIndex, int width, int height)
    {
        if (patchIndex < 0 || patchIndex >= Patches.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(patchIndex));
        }

        var scaleX = width / TargetWidthMillimeters;
        var scaleY = height / TargetHeightMillimeters;
        var patchRow = PatchRowRect(scaleX, scaleY);
        var patchWidth = patchRow.Width / (double)Patches.Count;
        var left = patchRow.X + patchWidth * patchIndex;
        var centerX = left + patchWidth / 2.0;
        var centerY = patchRow.Y + patchRow.Height / 2.0;

        return new MunsellLinearGrayscalePatchLayout(
            patchIndex,
            (int)Math.Round(left),
            patchRow.Y,
            Math.Max(1, (int)Math.Round(patchWidth)),
            patchRow.Height,
            (int)Math.Round(centerX),
            (int)Math.Round(centerY));
    }

    private static void DrawOuterBorder(Mat image, double scaleX, double scaleY)
    {
        var border = RectFromMillimeters(1.4, 1.4, TargetWidthMillimeters - 2.8, TargetHeightMillimeters - 2.8, scaleX, scaleY);
        Cv2.Rectangle(image, border, Color(174, 171, 163), Math.Max(1, ScaleAverage(0.18, scaleX, scaleY)), LineTypes.AntiAlias);
    }

    private static void DrawTitle(Mat image, double scaleX, double scaleY)
    {
        PutText(image, "MUNSELL LINEAR GRAYSCALE", 6.0, 6.1, 0.08, 1, Color(56, 56, 54), scaleX, scaleY);
        PutRightText(image, "Theoretical L* reference", TargetWidthMillimeters - 6.0, 6.1, 0.06, 1, Color(91, 88, 82), scaleX, scaleY);
    }

    private static IReadOnlyList<Rect> DrawPatchRow(Mat image, double scaleX, double scaleY)
    {
        var row = PatchRowRect(scaleX, scaleY);
        var rects = new List<Rect>(Patches.Count);
        Cv2.Rectangle(image, row, Color(38, 38, 36), Math.Max(1, ScaleAverage(0.16, scaleX, scaleY)), LineTypes.AntiAlias);

        for (var i = 0; i < Patches.Count; i++)
        {
            var patch = Patches[i];
            var x0 = row.X + (int)Math.Round(i * row.Width / (double)Patches.Count);
            var x1 = row.X + (int)Math.Round((i + 1) * row.Width / (double)Patches.Count);
            var rect = new Rect(x0, row.Y, Math.Max(1, x1 - x0), row.Height);
            var pixel = patch.EncodedRgb;
            rects.Add(rect);
            Cv2.Rectangle(image, rect, Color(pixel, pixel, pixel), -1, LineTypes.AntiAlias);
            Cv2.Rectangle(image, rect, Color(48, 48, 46), Math.Max(1, ScaleAverage(0.10, scaleX, scaleY)), LineTypes.AntiAlias);
        }

        return rects;
    }

    private static void DrawPatchLabels(Mat image, double scaleX, double scaleY)
    {
        var row = PatchRowRect(scaleX, scaleY);
        for (var i = 0; i < Patches.Count; i++)
        {
            var centerX = (row.X + (i + 0.5) * row.Width / Patches.Count) / scaleX;
            PutCenteredText(image, Patches[i].Label, centerX, 25.5, 0.055, 1, Color(70, 68, 64), scaleX, scaleY);
        }
    }

    private static void DrawMillimeterScale(Mat image, double scaleX, double scaleY)
    {
        const double startX = 18.0;
        const double endX = 237.0;
        const double baselineY = 29.0;

        Cv2.Line(image, Point(startX, baselineY, scaleX, scaleY), Point(endX, baselineY, scaleX, scaleY), Color(75, 72, 68), Math.Max(1, ScaleAverage(0.10, scaleX, scaleY)), LineTypes.AntiAlias);

        for (var millimeter = 0; millimeter <= 220; millimeter++)
        {
            var x = startX + millimeter;
            var length = millimeter % 10 == 0 ? 1.8 : millimeter % 5 == 0 ? 1.2 : 0.65;
            var thickness = millimeter % 10 == 0 ? Math.Max(1, ScaleAverage(0.12, scaleX, scaleY)) : 1;
            Cv2.Line(image, Point(x, baselineY, scaleX, scaleY), Point(x, baselineY - length, scaleX, scaleY), Color(75, 72, 68), thickness, LineTypes.AntiAlias);

            if (millimeter % 50 == 0)
            {
                PutCenteredText(image, millimeter.ToString(System.Globalization.CultureInfo.InvariantCulture), x, 31.2, 0.045, 1, Color(75, 72, 68), scaleX, scaleY);
            }
        }
    }

    private static Rect PatchRowRect(double scaleX, double scaleY)
    {
        return RectFromMillimeters(18.0, 10.0, 219.0, 11.5, scaleX, scaleY);
    }

    private static Rect RectFromMillimeters(double x, double y, double width, double height, double scaleX, double scaleY)
    {
        return new Rect(
            ScaleX(x, scaleX),
            ScaleY(y, scaleY),
            Math.Max(1, ScaleX(width, scaleX)),
            Math.Max(1, ScaleY(height, scaleY)));
    }

    private static Point Point(double x, double y, double scaleX, double scaleY)
    {
        return new Point(ScaleX(x, scaleX), ScaleY(y, scaleY));
    }

    private static void PutText(Mat image, string text, double x, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = fontScale * (scaleX + scaleY) / 2.0;
        var scaledThickness = Math.Max(1, thickness);
        Cv2.PutText(image, text, Point(x, baselineY, scaleX, scaleY), HersheyFonts.HersheySimplex, scaledFont, color, scaledThickness, LineTypes.AntiAlias);
    }

    private static void PutRightText(Mat image, string text, double rightX, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = fontScale * (scaleX + scaleY) / 2.0;
        var scaledThickness = Math.Max(1, thickness);
        var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scaledFont, scaledThickness, out _);
        var origin = Point(rightX, baselineY, scaleX, scaleY);
        origin.X -= size.Width;
        Cv2.PutText(image, text, origin, HersheyFonts.HersheySimplex, scaledFont, color, scaledThickness, LineTypes.AntiAlias);
    }

    private static void PutCenteredText(Mat image, string text, double centerX, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = fontScale * (scaleX + scaleY) / 2.0;
        var scaledThickness = Math.Max(1, thickness);
        var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scaledFont, scaledThickness, out _);
        var origin = Point(centerX, baselineY, scaleX, scaleY);
        origin.X -= size.Width / 2;
        Cv2.PutText(image, text, origin, HersheyFonts.HersheySimplex, scaledFont, color, scaledThickness, LineTypes.AntiAlias);
    }

    private static Scalar Color(byte red, byte green, byte blue)
    {
        return new Scalar(blue, green, red);
    }

    private static int PixelsForMillimeters(double millimeters, int dpi)
    {
        return (int)Math.Round(millimeters / 25.4 * dpi);
    }

    private static int ScaleX(double value, double scaleX)
    {
        return (int)Math.Round(value * scaleX);
    }

    private static int ScaleY(double value, double scaleY)
    {
        return (int)Math.Round(value * scaleY);
    }

    private static int ScaleAverage(double value, double scaleX, double scaleY)
    {
        return Math.Max(1, (int)Math.Round(value * (scaleX + scaleY) / 2.0));
    }

    private static void ValidateOptions(MunsellLinearGrayscaleTargetGeneratorOptions options)
    {
        if (options.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Dpi), "DPI must be positive.");
        }
    }
}

public sealed record MunsellLinearGrayscaleTargetGeneratorOptions
{
    public int Dpi { get; init; } = MunsellLinearGrayscaleTargetGenerator.DefaultDpi;

    public bool ShowLabels { get; init; } = true;

    public bool ShowMillimeterScale { get; init; } = true;

    public bool ShowTitle { get; init; } = true;

    public GrayscaleNoiseOptions? Noise { get; init; }
}

public sealed record MunsellLinearGrayscaleTargetGeneratorResult(string OutputPath, int Width, int Height, int Dpi);

public sealed record MunsellLinearGrayscalePatch(string Label, double LStar)
{
    public byte EncodedRgb => (byte)Math.Clamp((int)Math.Round(LStar / 100.0 * 255.0), 0, 255);

    public string Hex => $"#{EncodedRgb:X2}{EncodedRgb:X2}{EncodedRgb:X2}";
}

public sealed record MunsellLinearGrayscalePatchLayout(int PatchIndex, int X, int Y, int Width, int Height, int CenterX, int CenterY);
