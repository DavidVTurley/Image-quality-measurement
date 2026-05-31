using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Common;

internal static class MeasurementGeometry
{
    public static Rect CenteredSquare(int imageWidth, int imageHeight, int sampleSize, double centerX, double centerY)
    {
        var x = MeasurementMath.Clamp((int)Math.Round(centerX - sampleSize / 2.0), 0, imageWidth - sampleSize);
        var y = MeasurementMath.Clamp((int)Math.Round(centerY - sampleSize / 2.0), 0, imageHeight - sampleSize);

        return new Rect(x, y, sampleSize, sampleSize);
    }
}
