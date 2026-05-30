namespace Imcheck.Measurement.Meaasurements.Qa62;

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
}
