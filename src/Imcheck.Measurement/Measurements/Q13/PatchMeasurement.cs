namespace Imcheck.Measurement.Measurements.Q13;

public sealed record PatchMeasurement(
    int Index,
    double InputRed,
    double InputGreen,
    double InputBlue,
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
}
