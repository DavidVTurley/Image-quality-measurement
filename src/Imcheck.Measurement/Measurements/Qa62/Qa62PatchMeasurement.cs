namespace Imcheck.Measurement.Measurements.Qa62;

public sealed record Qa62PatchMeasurement(
    int Step,
    double OutputRed,
    double OutputGreen,
    double OutputBlue,
    double NoiseRed,
    double NoiseGreen,
    double NoiseBlue,
    double SampleCenterX,
    double SampleCenterY,
    int SampleX,
    int SampleY,
    int SampleSize)
{
    public double Luminance => 0.2126 * OutputRed + 0.7152 * OutputGreen + 0.0722 * OutputBlue;

    public int SampleTopLeftX => SampleX;

    public int SampleTopLeftY => SampleY;

    public int SampleTopRightX => SampleX + SampleSize;

    public int SampleTopRightY => SampleY;

    public int SampleBottomRightX => SampleX + SampleSize;

    public int SampleBottomRightY => SampleY + SampleSize;

    public int SampleBottomLeftX => SampleX;

    public int SampleBottomLeftY => SampleY + SampleSize;
}
