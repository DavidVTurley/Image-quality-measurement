using OpenCvSharp;

namespace Imcheck.Measurement;

internal static class TargetRendering
{
    public static int PixelsForMillimeters(double millimeters, int dpi)
    {
        return (int)Math.Round(millimeters / 25.4 * dpi);
    }

    public static Rect Rect(double x, double y, double width, double height, double scaleX, double scaleY)
    {
        return new Rect(
            ScaleX(x, scaleX),
            ScaleY(y, scaleY),
            Math.Max(1, ScaleX(width, scaleX)),
            Math.Max(1, ScaleY(height, scaleY)));
    }

    public static Point Point(double x, double y, double scaleX, double scaleY)
    {
        return new Point(ScaleX(x, scaleX), ScaleY(y, scaleY));
    }

    public static Scalar Color((byte Red, byte Green, byte Blue) color)
    {
        return Color(color.Red, color.Green, color.Blue);
    }

    public static Scalar Color(byte red, byte green, byte blue)
    {
        return new Scalar(blue, green, red);
    }

    public static int ScaleX(double value, double scaleX)
    {
        return (int)Math.Round(value * scaleX);
    }

    public static int ScaleY(double value, double scaleY)
    {
        return (int)Math.Round(value * scaleY);
    }

    public static int ScaleAverage(double value, double scaleX, double scaleY)
    {
        return Math.Max(1, (int)Math.Round(value * (scaleX + scaleY) / 2.0));
    }

    public static int ScaleThickness(int thickness, double scaleX, double scaleY)
    {
        return Math.Max(1, (int)Math.Round(thickness * (scaleX + scaleY) / 2.0));
    }

    public static void PutText(Mat image, string text, double x, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = ScaledFont(fontScale, scaleX, scaleY);
        Cv2.PutText(image, text, Point(x, baselineY, scaleX, scaleY), HersheyFonts.HersheySimplex, scaledFont, color, Math.Max(1, thickness), LineTypes.AntiAlias);
    }

    public static void PutScaledText(Mat image, string text, double x, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = ScaledFont(fontScale, scaleX, scaleY);
        Cv2.PutText(image, text, Point(x, baselineY, scaleX, scaleY), HersheyFonts.HersheySimplex, scaledFont, color, ScaleThickness(thickness, scaleX, scaleY), LineTypes.AntiAlias);
    }

    public static void PutRightText(Mat image, string text, double rightX, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = ScaledFont(fontScale, scaleX, scaleY);
        var scaledThickness = Math.Max(1, thickness);
        var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scaledFont, scaledThickness, out _);
        var origin = Point(rightX, baselineY, scaleX, scaleY);
        origin.X -= size.Width;
        Cv2.PutText(image, text, origin, HersheyFonts.HersheySimplex, scaledFont, color, scaledThickness, LineTypes.AntiAlias);
    }

    public static void PutCenteredText(Mat image, string text, double centerX, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = ScaledFont(fontScale, scaleX, scaleY);
        var scaledThickness = Math.Max(1, thickness);
        var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scaledFont, scaledThickness, out _);
        var origin = Point(centerX, baselineY, scaleX, scaleY);
        origin.X -= size.Width / 2;
        Cv2.PutText(image, text, origin, HersheyFonts.HersheySimplex, scaledFont, color, scaledThickness, LineTypes.AntiAlias);
    }

    public static void PutScaledCenteredText(Mat image, string text, double centerX, double baselineY, double fontScale, int thickness, Scalar color, double scaleX, double scaleY)
    {
        var scaledFont = ScaledFont(fontScale, scaleX, scaleY);
        var scaledThickness = ScaleThickness(thickness, scaleX, scaleY);
        var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scaledFont, scaledThickness, out _);
        var origin = Point(centerX, baselineY, scaleX, scaleY);
        origin.X -= size.Width / 2;
        Cv2.PutText(image, text, origin, HersheyFonts.HersheySimplex, scaledFont, color, scaledThickness, LineTypes.AntiAlias);
    }

    public static void EnsureOutputDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static double ScaledFont(double fontScale, double scaleX, double scaleY)
    {
        return fontScale * (scaleX + scaleY) / 2.0;
    }
}
