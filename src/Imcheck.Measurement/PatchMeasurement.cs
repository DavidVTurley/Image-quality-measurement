namespace Imcheck.Measurement;

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
    bool IsColor)
{
    public double Output => OutputGreen;

    public double Noise => NoiseGreen;
}
