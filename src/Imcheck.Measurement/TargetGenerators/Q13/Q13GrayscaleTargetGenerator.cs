using OpenCvSharp;

namespace Imcheck.Measurement;

public sealed class Q13GrayscaleTargetGenerator
{
    public const int DefaultDpi = 600;
    public const double TargetWidthMillimeters = 203.0;
    public const double TargetHeightMillimeters = 30.0;

    public static IReadOnlyList<Q13GrayscalePatch> Patches { get; } = Enumerable.Range(0, 20)
        .Select(index =>
        {
            var density = 0.05 + index * 0.10;
            var label = index switch
            {
                0 => "A",
                7 => "M",
                16 => "B",
                _ => index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            return new Q13GrayscalePatch(index, label, density, DensityToLStar(density));
        })
        .ToList();

    public Q13GrayscaleTargetGeneratorResult Generate(string outputPath, Q13GrayscaleTargetGeneratorOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        options ??= new Q13GrayscaleTargetGeneratorOptions();
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
            throw new InvalidOperationException($"Unable to write generated Q13 grayscale target: {outputPath}");
        }

        return new Q13GrayscaleTargetGeneratorResult(outputPath, width, height, options.Dpi);
    }

    public Mat Render(int width, int height, Q13GrayscaleTargetGeneratorOptions? options = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        options ??= new Q13GrayscaleTargetGeneratorOptions();
        ValidateOptions(options);

        var scaleX = width / TargetWidthMillimeters;
        var scaleY = height / TargetHeightMillimeters;
        var image = new Mat(height, width, MatType.CV_8UC3, Color(246, 245, 241));
        var patchRects = DrawPatchRow(image, scaleX, scaleY);

        GrayscaleNoiseApplicator.Apply(image, patchRects, options.Noise);

        if (options.ShowTitle)
        {
            DrawTitle(image, scaleX, scaleY);
        }

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

    public static Q13GrayscalePatchLayout GetPatchLayout(int patchIndex, int width, int height)
    {
        if (patchIndex < 0 || patchIndex >= Patches.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(patchIndex));
        }

        var scaleX = width / TargetWidthMillimeters;
        var scaleY = height / TargetHeightMillimeters;
        var row = PatchRowRect(scaleX, scaleY);
        var patchWidth = row.Width / (double)Patches.Count;
        var left = row.X + patchWidth * patchIndex;
        var centerX = left + patchWidth / 2.0;
        var centerY = row.Y + row.Height / 2.0;

        return new Q13GrayscalePatchLayout(
            patchIndex,
            (int)Math.Round(left),
            row.Y,
            Math.Max(1, (int)Math.Round(patchWidth)),
            row.Height,
            (int)Math.Round(centerX),
            (int)Math.Round(centerY));
    }

    private static IReadOnlyList<Rect> DrawPatchRow(Mat image, double scaleX, double scaleY)
    {
        var row = PatchRowRect(scaleX, scaleY);
        var rects = new List<Rect>(Patches.Count);

        Cv2.Rectangle(image, row, Color(42, 42, 40), Math.Max(1, ScaleAverage(0.12, scaleX, scaleY)), LineTypes.AntiAlias);
        for (var i = 0; i < Patches.Count; i++)
        {
            var x0 = row.X + (int)Math.Round(i * row.Width / (double)Patches.Count);
            var x1 = row.X + (int)Math.Round((i + 1) * row.Width / (double)Patches.Count);
            var rect = new Rect(x0, row.Y, Math.Max(1, x1 - x0), row.Height);
            var pixel = Patches[i].EncodedRgb;
            rects.Add(rect);
            Cv2.Rectangle(image, rect, Color(pixel, pixel, pixel), -1, LineTypes.AntiAlias);
            Cv2.Rectangle(image, rect, Color(45, 45, 43), 1, LineTypes.AntiAlias);
        }

        return rects;
    }

    private static void DrawTitle(Mat image, double scaleX, double scaleY)
    {
        PutText(image, "KODAK Q13 GRAYSCALE", 5.0, 6.0, 0.075, 1, Color(56, 56, 54), scaleX, scaleY);
        PutRightText(image, "D=0.05 to 1.95  dD=0.10", TargetWidthMillimeters - 5.0, 6.0, 0.055, 1, Color(91, 88, 82), scaleX, scaleY);
    }

    private static void DrawPatchLabels(Mat image, double scaleX, double scaleY)
    {
        var row = PatchRowRect(scaleX, scaleY);
        for (var i = 0; i < Patches.Count; i++)
        {
            var centerX = (row.X + (i + 0.5) * row.Width / Patches.Count) / scaleX;
            PutCenteredText(image, Patches[i].Label, centerX, 25.0, 0.055, 1, Color(70, 68, 64), scaleX, scaleY);
        }
    }

    private static void DrawMillimeterScale(Mat image, double scaleX, double scaleY)
    {
        const double startX = 8.0;
        const double endX = 195.0;
        const double baselineY = 28.0;

        Cv2.Line(image, Point(startX, baselineY, scaleX, scaleY), Point(endX, baselineY, scaleX, scaleY), Color(75, 72, 68), 1, LineTypes.AntiAlias);
        for (var millimeter = 0; millimeter <= 185; millimeter++)
        {
            var x = startX + millimeter;
            var length = millimeter % 10 == 0 ? 1.7 : millimeter % 5 == 0 ? 1.15 : 0.6;
            Cv2.Line(image, Point(x, baselineY, scaleX, scaleY), Point(x, baselineY - length, scaleX, scaleY), Color(75, 72, 68), 1, LineTypes.AntiAlias);

            if (millimeter % 50 == 0)
            {
                PutCenteredText(image, millimeter.ToString(System.Globalization.CultureInfo.InvariantCulture), x, 29.8, 0.04, 1, Color(75, 72, 68), scaleX, scaleY);
            }
        }
    }

    private static Rect PatchRowRect(double scaleX, double scaleY)
    {
        return RectFromMillimeters(8.0, 8.0, 187.0, 13.2, scaleX, scaleY);
    }

    private static double DensityToLStar(double density)
    {
        var y = Math.Pow(10, -density);
        return y > 0.008856
            ? 116.0 * Math.Pow(y, 1.0 / 3.0) - 16.0
            : 9.033 * y * 100.0;
    }

    private static Rect RectFromMillimeters(double x, double y, double width, double height, double scaleX, double scaleY)
    {
        return new Rect(ScaleX(x, scaleX), ScaleY(y, scaleY), Math.Max(1, ScaleX(width, scaleX)), Math.Max(1, ScaleY(height, scaleY)));
    }

    private static Point Point(double x, double y, double scaleX, double scaleY)
    {
        return new Point(ScaleX(x, scaleX), ScaleY(y, scaleY));
    }

    private static void PutText(Mat image, string text, double x, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = fontScale * (scaleX + scaleY) / 2.0;
        Cv2.PutText(image, text, Point(x, baselineY, scaleX, scaleY), HersheyFonts.HersheySimplex, scaledFont, color, Math.Max(1, thickness), LineTypes.AntiAlias);
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

    private static int ScaleX(double value, double scaleX) => (int)Math.Round(value * scaleX);

    private static int ScaleY(double value, double scaleY) => (int)Math.Round(value * scaleY);

    private static int ScaleAverage(double value, double scaleX, double scaleY) => Math.Max(1, (int)Math.Round(value * (scaleX + scaleY) / 2.0));

    private static void ValidateOptions(Q13GrayscaleTargetGeneratorOptions options)
    {
        if (options.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Dpi), "DPI must be positive.");
        }
    }
}

public sealed record Q13GrayscaleTargetGeneratorOptions
{
    public int Dpi { get; init; } = Q13GrayscaleTargetGenerator.DefaultDpi;

    public bool ShowLabels { get; init; }

    public bool ShowMillimeterScale { get; init; }

    public bool ShowTitle { get; init; }

    public GrayscaleNoiseOptions? Noise { get; init; }
}

public sealed record Q13GrayscaleTargetGeneratorResult(string OutputPath, int Width, int Height, int Dpi);

public sealed record Q13GrayscalePatch(int Index, string Label, double Density, double LStar)
{
    public byte EncodedRgb => (byte)Math.Clamp((int)Math.Round(LStar / 100.0 * 255.0), 0, 255);

    public string Hex => $"#{EncodedRgb:X2}{EncodedRgb:X2}{EncodedRgb:X2}";
}

public sealed record Q13GrayscalePatchLayout(int PatchIndex, int X, int Y, int Width, int Height, int CenterX, int CenterY);
