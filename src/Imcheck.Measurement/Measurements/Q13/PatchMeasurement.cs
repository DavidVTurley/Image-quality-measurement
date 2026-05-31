namespace Imcheck.Measurement.Measurements.Q13;

public sealed record PatchMeasurement(
    int Index,
    double OutputRed,
    double OutputGreen,
    double OutputBlue,
    double NoiseRed,
    double NoiseGreen,
    double NoiseBlue,
    bool IsColor,
    double SampleCenterX,
    double SampleCenterY,
    int SampleX,
    int SampleY,
    int SampleSize)
{
    public double Output => OutputGreen;

    public double Noise => NoiseGreen;

    public int SampleTopLeftX => SampleX;

    public int SampleTopLeftY => SampleY;

    public int SampleTopRightX => SampleX + SampleSize;

    public int SampleTopRightY => SampleY;

    public int SampleBottomRightX => SampleX + SampleSize;

    public int SampleBottomRightY => SampleY + SampleSize;

    public int SampleBottomLeftX => SampleX;

    public int SampleBottomLeftY => SampleY + SampleSize;
}
